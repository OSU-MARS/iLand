using Apache.Arrow;
using Apache.Arrow.Types;
using iLand.Extensions;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;

namespace iLand.Output.Memory
{
    internal class ResourceUnitThreePGArrowMemory : ArrowMemory
    {
        private readonly IntegerType treeSpeciesFieldType;

        private readonly byte[] resourceUnitID;
        private readonly byte[] treeSpeciesIndices;
        private readonly byte[] calendarYear;
        private readonly byte[] month;
        private readonly byte[] solarRadiation;
        private readonly byte[] utilizablePar;
        private readonly byte[] monthlyGpp;
        private readonly byte[] co2Modifier;
        // if needed, the resource unit's annual nitrogen modifier can be included
        private readonly byte[] soilWaterModifier;
        private readonly byte[] temperatureModifier;
        private readonly byte[] vpdModifier;

        public RecordBatch RecordBatch { get; private init; }

        public ResourceUnitThreePGArrowMemory(IntegerType treeSpeciesFieldType, int batchLength)
        {
            this.treeSpeciesFieldType = treeSpeciesFieldType;

            this.resourceUnitID = new byte[batchLength * sizeof(Int32)];
            this.treeSpeciesIndices = new byte[batchLength * treeSpeciesFieldType.BitWidth / 8];
            this.calendarYear = new byte[batchLength * sizeof(Int16)];
            this.month = new byte[batchLength * sizeof(byte)];
            this.solarRadiation = new byte[batchLength * sizeof(Int32)];
            this.utilizablePar = new byte[batchLength * sizeof(float)];
            this.monthlyGpp = new byte[batchLength * sizeof(float)];
            this.co2Modifier = new byte[batchLength * sizeof(float)];
            this.soilWaterModifier = new byte[batchLength * sizeof(float)];
            this.temperatureModifier = new byte[batchLength * sizeof(float)];
            this.vpdModifier = new byte[batchLength * sizeof(float)];

            // create schema
            List<Field> fields = new()
            {
                new("resourceUnit", Int32Type.Default, false),
                new("species", treeSpeciesFieldType, false),
                new("year", Int16Type.Default, false),
                new("month", UInt8Type.Default, false),
                new("solarRadiation", FloatType.Default, false),
                new("utilizablePar", FloatType.Default, false),
                new("monthlyGpp", FloatType.Default, false),
                new("co2Modifier", FloatType.Default, false),
                new("soilWaterModifier", FloatType.Default, false),
                new("temperatureModifier", FloatType.Default, false),
                new("vpdModifier", FloatType.Default, false)
            };

            Dictionary<string, string> metadata = new()
            {
                { "_esourceUnit", "Resource unit's numeric ID." }, // work around https://issues.apache.org/jira/browse/ARROW-17466
                { "species", "Integer code for tree species, typically either a USFS FIA code (US Forest Service Forest Inventory and Analysis, 16 bit) or ITIS TSN (Integrated Taxonomic Information System taxonmic serial number, 32 bit)." },
                { "year", "Calendar year." },
                { "month", "Month of year." },
                { "solarRadiation", "Monthly total radiation sum in MJ/m²." },
                { "utilizablePar", "Monthly photosynthetically active radiation multiplied by minimum of soil water, temperature, and VPD modifiers." },
                { "monthlyGpp", "Monthly gross primary production, kg biomass/m²." },
                { "co2Modifier", "Monthly carbon dioxide growth modifier." },
                { "soilWaterModifier", "Monthly soil water growth modifier." },
                { "temperatureModifier", "Monthly temperature growth modifer." },
                { "vpdModifier", "Monthly vapor pressure deficit growth modifier." }
            };
            Schema schema = new(fields, metadata);

            // repackage arrays into Arrow record batch
            IArrowArray[] arrowArrays = new IArrowArray[]
            {
                ArrowArrayExtensions.WrapInInt32(this.resourceUnitID),
                // not supported in Apache 9.0.0
                // ArrowArrayExtensions.BindStringTable256(this.treeSpeciesIndices, treeSpecies),
                ArrowArrayExtensions.Wrap(treeSpeciesFieldType, this.treeSpeciesIndices),
                ArrowArrayExtensions.WrapInInt16(this.calendarYear),
                ArrowArrayExtensions.WrapInUInt8(this.month),
                ArrowArrayExtensions.WrapInFloat(this.solarRadiation),
                ArrowArrayExtensions.WrapInFloat(this.utilizablePar),
                ArrowArrayExtensions.WrapInFloat(this.monthlyGpp),
                ArrowArrayExtensions.WrapInFloat(this.co2Modifier),
                ArrowArrayExtensions.WrapInFloat(this.soilWaterModifier),
                ArrowArrayExtensions.WrapInFloat(this.temperatureModifier),
                ArrowArrayExtensions.WrapInFloat(this.vpdModifier)
            };

            this.RecordBatch = new(schema, arrowArrays, batchLength);
        }

        public void Add(ResourceUnitThreePGTimeSeries threePGtimeSeries, int resourceUnitID, int treeSpeciesCode, int calendarYearBeforeFirstSimulationTimestep)
        {
            int monthsInTimeSeries = threePGtimeSeries.LengthInMonths;

            this.Fill(this.resourceUnitID, resourceUnitID, monthsInTimeSeries);
            this.Fill(this.treeSpeciesIndices, this.treeSpeciesFieldType, treeSpeciesCode, monthsInTimeSeries);

            Span<byte> monthsOfYear = stackalloc byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            Int16 calendarYear = (Int16)calendarYearBeforeFirstSimulationTimestep;
            int yearsInTimeSeries = monthsInTimeSeries / 12;
            for (int simulationYear = 0, yearStartIndex = this.Count; simulationYear < yearsInTimeSeries; ++calendarYear, ++simulationYear, yearStartIndex += Constant.MonthsInYear)
            {
                MemoryMarshal.Cast<byte, Int16>(this.calendarYear.AsSpan()).Slice(yearStartIndex, Constant.MonthsInYear).Fill(calendarYear);
                monthsOfYear.CopyTo(this.month.AsSpan().Slice(yearStartIndex, Constant.MonthsInYear));
            }

            this.CopyFirstN(threePGtimeSeries.SolarRadiationTotal, this.solarRadiation, monthsInTimeSeries);
            this.CopyFirstN(threePGtimeSeries.UtilizablePar, this.utilizablePar, monthsInTimeSeries);
            this.CopyFirstN(threePGtimeSeries.MonthlyGpp, this.monthlyGpp, monthsInTimeSeries);
            this.CopyFirstN(threePGtimeSeries.CO2Modifier, this.co2Modifier, monthsInTimeSeries);
            this.CopyFirstN(threePGtimeSeries.SoilWaterModifier, this.soilWaterModifier, monthsInTimeSeries);
            this.CopyFirstN(threePGtimeSeries.TemperatureModifier, this.temperatureModifier, monthsInTimeSeries);
            this.CopyFirstN(threePGtimeSeries.VpdModifier, this.vpdModifier, monthsInTimeSeries);

            this.Count += monthsInTimeSeries;
        }
    }
}
