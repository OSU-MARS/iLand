using Apache.Arrow;
using Apache.Arrow.Types;
using iLand.Extensions;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace iLand.Output.Memory
{
    internal class ResourceUnitThreePGArrowMemory : ArrowMemory
    {
        // for now, must be a multiple of 12 as splitting years across batches is not supported by Add()
        // Batch size should be small enough to avoid https://github.com/apache/arrow/issues/37069 (2 GB is ~44M records at 49 bytes/record).
        private const int MaximumRecordsPerBatch = 12 * 1000 * 1000;

        private readonly Schema schema;
        private readonly IntegerType treeSpeciesFieldType;

        private byte[]? resourceUnitID;
        private byte[]? treeSpeciesIndices;
        private byte[]? calendarYear;
        private byte[]? month;
        private byte[]? solarRadiation;
        private byte[]? utilizablePar;
        private byte[]? monthlyGpp;
        private byte[]? co2Modifier;
        private byte[]? evapotranspiration;
        // if needed, the resource unit's annual nitrogen modifier can be included
        private byte[]? soilWaterInfiltration;
        private byte[]? soilWaterModifier;
        private byte[]? soilWaterPotential;
        private byte[]? temperatureModifier;
        private byte[]? vpdModifier;

        public ResourceUnitThreePGArrowMemory(IntegerType treeSpeciesFieldType, int capacityInRecords)
            : base(capacityInRecords, ResourceUnitThreePGArrowMemory.MaximumRecordsPerBatch)
        {
            this.treeSpeciesFieldType = treeSpeciesFieldType;

            // create schema
            List<Field> fields =
            [
                new("resourceUnit", Int32Type.Default, false),
                new("species", treeSpeciesFieldType, false),
                new("year", Int16Type.Default, false),
                new("month", UInt8Type.Default, false),
                new("solarRadiation", FloatType.Default, false),
                new("infiltration", FloatType.Default, false),
                new("evapotranspiration", FloatType.Default, false),
                new("soilWaterPotential", FloatType.Default, false),
                new("utilizablePar", FloatType.Default, false),
                new("monthlyGpp", FloatType.Default, false),
                new("co2Modifier", FloatType.Default, false),
                new("soilWaterModifier", FloatType.Default, false),
                new("temperatureModifier", FloatType.Default, false),
                new("vpdModifier", FloatType.Default, false)
            ];

            Dictionary<string, string> metadata = new()
            {
                { "_esourceUnit", "Resource unit's numeric ID." }, // work around https://github.com/apache/arrow/issues/32729
                { "species", "Integer code for tree species, typically either a USFS FIA code (US Forest Service Forest Inventory and Analysis, 16 bit) or WFO ID (World Flora Online identifier, 32 bit)." },
                { "year", "Calendar year." },
                { "month", "Month of year." },
                { "solarRadiation", "Monthly total radiation sum in MJ/m²." },
                { "infiltration", "Monthly total infiltration, mm water column." },
                { "evapotranspiration", "Monthly total evapotranspiration, mm water column." },
                { "soilWaterPotential", "Monthly matric potential, kPa." },
                { "utilizablePar", "Monthly photosynthetically active radiation multiplied by minimum of soil water, temperature, and VPD modifiers." },
                { "monthlyGpp", "Monthly gross primary production, kg biomass/m²." },
                { "co2Modifier", "Monthly carbon dioxide growth modifier." },
                { "soilWaterModifier", "Monthly soil water growth modifier." },
                { "temperatureModifier", "Monthly temperature growth modifer." },
                { "vpdModifier", "Monthly vapor pressure deficit growth modifier." }
            };
            this.schema = new(fields, metadata);

            this.resourceUnitID = null;
            this.treeSpeciesIndices = null;
            this.calendarYear = null;
            this.month = null;
            this.solarRadiation = null;
            this.utilizablePar = null;
            this.monthlyGpp = null;
            this.co2Modifier = null;
            this.evapotranspiration = null;
            this.soilWaterInfiltration = null;
            this.soilWaterModifier = null;
            this.soilWaterPotential = null;
            this.temperatureModifier = null;
            this.vpdModifier = null;
        }

        public void Add(ResourceUnitThreePGTimeSeries threePGtimeSeries, UInt32 resourceUnitID, UInt32 treeSpeciesCode, int calendarYearBeforeFirstSimulationTimestep)
        {
            int monthsInTimeSeries = threePGtimeSeries.LengthInMonths;
            Int16 calendarYear = (Int16)calendarYearBeforeFirstSimulationTimestep;

            (int startIndexInRecordBatch, int monthsToCopyToRecordBatch) = this.GetBatchIndicesForAdd(monthsInTimeSeries);
            if (startIndexInRecordBatch == 0)
            {
                this.AppendNewBatch();
            }
            this.Add(threePGtimeSeries, resourceUnitID, treeSpeciesCode, startIndexInRecordBatch, monthsToCopyToRecordBatch, ref calendarYear);

            int monthsRemainingToCopy = monthsInTimeSeries - monthsToCopyToRecordBatch;
            if (monthsRemainingToCopy > 0)
            {
                this.AppendNewBatch();
                this.Add(threePGtimeSeries, resourceUnitID, treeSpeciesCode, 0, monthsRemainingToCopy, ref calendarYear);
            }
        }

        private void Add(ResourceUnitThreePGTimeSeries threePGtimeSeries, UInt32 resourceUnitID, UInt32 treeSpeciesCode, int startIndexInRecordBatch, int monthsToCopy, ref Int16 calendarYear)
        {
            Debug.Assert(monthsToCopy % 12 == 0, monthsToCopy + " months to add is not an integer multiple of years."); // code below assumes time series contain complete years (January-December) and year boundaries align with record batch boundaries

            ArrowMemory.Fill(this.resourceUnitID, resourceUnitID, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.Fill(this.treeSpeciesIndices, this.treeSpeciesFieldType, treeSpeciesCode, startIndexInRecordBatch, monthsToCopy);

            Span<byte> monthsOfYear = [ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 ];
            int yearsInTimeSeries = monthsToCopy / 12;
            for (int simulationYear = 0, yearStartIndex = startIndexInRecordBatch; simulationYear < yearsInTimeSeries; ++calendarYear, ++simulationYear, yearStartIndex += Constant.Time.MonthsInYear)
            {
                MemoryMarshal.Cast<byte, Int16>(this.calendarYear.AsSpan()).Slice(yearStartIndex, Constant.Time.MonthsInYear).Fill(calendarYear);
                monthsOfYear.CopyTo(this.month.AsSpan().Slice(yearStartIndex, Constant.Time.MonthsInYear));
            }

            ArrowMemory.CopyFirstN(threePGtimeSeries.SolarRadiationTotal, this.solarRadiation, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.CopyFirstN(threePGtimeSeries.SoilWaterInfiltration, this.soilWaterInfiltration, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.CopyFirstN(threePGtimeSeries.Evapotranspiration, this.evapotranspiration, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.CopyFirstN(threePGtimeSeries.SoilWaterPotential, this.soilWaterPotential, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.CopyFirstN(threePGtimeSeries.UtilizablePar, this.utilizablePar, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.CopyFirstN(threePGtimeSeries.MonthlyGpp, this.monthlyGpp, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.CopyFirstN(threePGtimeSeries.CO2Modifier, this.co2Modifier, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.CopyFirstN(threePGtimeSeries.SoilWaterModifier, this.soilWaterModifier, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.CopyFirstN(threePGtimeSeries.TemperatureModifier, this.temperatureModifier, startIndexInRecordBatch, monthsToCopy);
            ArrowMemory.CopyFirstN(threePGtimeSeries.VpdModifier, this.vpdModifier, startIndexInRecordBatch, monthsToCopy);

            this.Count += monthsToCopy;
        }

        private void AppendNewBatch()
        {
            int batchLength = this.GetNextBatchLength();

            this.resourceUnitID = new byte[batchLength * sizeof(Int32)];
            this.treeSpeciesIndices = new byte[batchLength * treeSpeciesFieldType.BitWidth / 8];
            this.calendarYear = new byte[batchLength * sizeof(Int16)];
            this.month = new byte[batchLength * sizeof(byte)];
            this.solarRadiation = new byte[batchLength * sizeof(Int32)];
            this.utilizablePar = new byte[batchLength * sizeof(float)];
            this.monthlyGpp = new byte[batchLength * sizeof(float)];
            this.co2Modifier = new byte[batchLength * sizeof(float)];
            this.evapotranspiration = new byte[batchLength * sizeof(float)];
            this.soilWaterInfiltration = new byte[batchLength * sizeof(float)];
            this.soilWaterModifier = new byte[batchLength * sizeof(float)];
            this.soilWaterPotential = new byte[batchLength * sizeof(float)];
            this.temperatureModifier = new byte[batchLength * sizeof(float)];
            this.vpdModifier = new byte[batchLength * sizeof(float)];

            // repackage arrays into Arrow record batch
            IArrowArray[] arrowArrays =
            [
                ArrowArrayExtensions.WrapInInt32(this.resourceUnitID),
                // not supported in Apache 9.0.0
                // ArrowArrayExtensions.BindStringTable256(this.treeSpeciesIndices, treeSpecies),
                ArrowArrayExtensions.Wrap(treeSpeciesFieldType, this.treeSpeciesIndices),
                ArrowArrayExtensions.WrapInInt16(this.calendarYear),
                ArrowArrayExtensions.WrapInUInt8(this.month),
                ArrowArrayExtensions.WrapInFloat(this.solarRadiation),
                ArrowArrayExtensions.WrapInFloat(this.soilWaterInfiltration),
                ArrowArrayExtensions.WrapInFloat(this.evapotranspiration),
                ArrowArrayExtensions.WrapInFloat(this.soilWaterPotential),
                ArrowArrayExtensions.WrapInFloat(this.utilizablePar),
                ArrowArrayExtensions.WrapInFloat(this.monthlyGpp),
                ArrowArrayExtensions.WrapInFloat(this.co2Modifier),
                ArrowArrayExtensions.WrapInFloat(this.soilWaterModifier),
                ArrowArrayExtensions.WrapInFloat(this.temperatureModifier),
                ArrowArrayExtensions.WrapInFloat(this.vpdModifier)
            ];

            this.RecordBatches.Add(new(this.schema, arrowArrays, batchLength));
        }
    }
}
