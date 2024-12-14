using Apache.Arrow;
using Apache.Arrow.Types;
using iLand.Extensions;
using System;
using System.Collections.Generic;

namespace iLand.Output.Memory
{
    internal class StandOrResourceUnitArrowMemory : ArrowMemory
    {
        private readonly IntegerType treeSpeciesFieldType;

        private readonly Schema schema;

        private byte[]? id;
        private byte[]? calendarYear;
        private byte[]? treeSpeciesIndices;
        private byte[]? averageDbh;
        private byte[]? averageHeight;
        private byte[]? liveStemVolume;
        private byte[]? treeBasalArea;
        private byte[]? treeLai;
        private byte[]? treeNpp;
        private byte[]? treeAbovegroundNpp;
        private byte[]? treesPerHectare;
        private byte[]? saplingBasalArea;
        private byte[]? saplingLai;
        private byte[]? saplingCohorts;
        private byte[]? saplingMeanAge;
        private byte[]? saplingNpp;
        private byte[]? saplingsPerHectare;
        private byte[]? branchCarbon;
        private byte[]? branchNitrogen;
        private byte[]? coarseRootCarbon;
        private byte[]? coarseRootNitrogen;
        private byte[]? fineRootCarbon;
        private byte[]? fineRootNitrogen;
        private byte[]? foliageCarbon;
        private byte[]? foliageNitrogen;
        private byte[]? regenerationCarbon;
        private byte[]? regenerationNitrogen;
        private byte[]? stemCarbon;
        private byte[]? stemNitrogen;

        public StandOrResourceUnitArrowMemory(string idFieldName, string idFieldDescription, IntegerType treeSpeciesFieldType, int capacityInRecords)
            : base(capacityInRecords, ArrowMemory.DefaultMaximumRecordsPerBatch)
        {
            this.treeSpeciesFieldType = treeSpeciesFieldType;

            // create schema
            List<Field> fields =
            [
                new(idFieldName, Int32Type.Default, false),
                new("year", Int16Type.Default, false),
                new("species", treeSpeciesFieldType, false),
                new("averageDbh", FloatType.Default, false),
                new("averageHeight", FloatType.Default, false),
                new("liveStemVolume", FloatType.Default, false),
                new("treeBasalArea", FloatType.Default, false),
                new("treeLai", FloatType.Default, false),
                new("treeNpp", FloatType.Default, false),
                new("treeAbovegroundNpp", FloatType.Default, false),
                new("treesPerHectare", FloatType.Default, false),
                new("saplingLai", FloatType.Default, false),
                new("saplingNpp", FloatType.Default, false),
                new("saplingCohorts", FloatType.Default, false),
                new("saplingMeanAge", FloatType.Default, false),
                new("saplingNpp", FloatType.Default, false),
                new("saplingsPerHectare", FloatType.Default, false),
                new("branchCarbon", FloatType.Default, false),
                new("branchNitrogen", FloatType.Default, false),
                new("coarseRootCarbon", FloatType.Default, false),
                new("coarseRootNitrogen", FloatType.Default, false),
                new("fineRootCarbon", FloatType.Default, false),
                new("fineRootNitrogen", FloatType.Default, false),
                new("foliageCarbon", FloatType.Default, false),
                new("foliageNitrogen", FloatType.Default, false),
                new("regenerationCarbon", FloatType.Default, false),
                new("regenerationNitrogen", FloatType.Default, false),
                new("stemCarbon", FloatType.Default, false),
                new("stemNitrogen", FloatType.Default, false)
            ];

            Dictionary<string, string> metadata = new()
            {
                { idFieldName, idFieldDescription },
                { "year", "Calendar year." },
                { "species", "Integer code for tree species, typically " + Constant.AllTreeSpeciesCode + " to indicate all tree species present, a USFS FIA code (US Forest Service Forest Inventory and Analysis, 16 bit), or WFO ID (World Flora Online identifier, 32 bit)." },
                { "averageDbh", "Arithmetic mean diameter of trees, cm." },
                { "averageHeight", "Arithmetic mean height of trees, m." },
                { "liveStemVolume", "Live stem volume of trees, , m³/ha" },
                { "treeBasalArea", "Basal area of trees, m²/ha." },
                { "treeLai", "Leaf area index of trees, m²/m²." },
                { "treeNpp", "Net primary production of trees, kg biomass/ha-yr." },
                { "treeAbovegroundNpp", "Net primary production of trees in aboveground compartments, kg biomass/ha-yr." },
                { "treesPerHectare", "Number of trees per hectare." },
                { "saplingBasalArea", "Basal area of trees, m²/ha." },
                { "saplingLai", "Leaf area index of trees, m²/m²." },
                { "saplingCohorts", "Number of cohorts of saplings per hectare." },
                { "saplingMeanAge", "Mean age of saplings in years." },
                { "saplingNpp", "Net primary production of saplings, kg biomass/ha-yr." },
                { "saplingsPerHectare", "Number of saplings per hectare." },
                { "branchCarbon", "Carbon contained in tree branches, kg/ha-yr." },
                { "branchNitrogen", "Nitrogen contained in tree branches, kg/ha-yr." },
                { "coarseRootCarbon", "Carbon contained in trees' coarse roots, kg/ha-yr." },
                { "coarseRootNitrogen", "Nitrogen contained in trees' coarse roots, kg/ha-yr." },
                { "fineRootCarbon", "Carbon contained in trees' fine roots, kg/ha-yr." },
                { "fineRootNitrogen", "Nitrogen contained in trees' fine roots, kg/ha-yr." },
                { "foliageCarbon", "Carbon contained in trees' foliage, kg/ha-yr." },
                { "foliageNitrogen", "Nitrogen contained in trees' foliage, kg/ha-yr." },
                { "regenerationCarbon", "Carbon contained in saplings, kg/ha-yr." },
                { "regenerationNitrogen", "Nitrogen contained in saplings, kg/ha-yr." },
                { "stemCarbon", "Carbon contained in live tree stems (snags are reported separately), kg/ha-yr." },
                { "stemNitrogen", "Nitrogen contained in live tree stems, kg/ha-yr." }
            };
            this.schema = new(fields, metadata);

            this.id = null;
            this.calendarYear = null;
            this.treeSpeciesIndices = null;
            this.averageDbh = null;
            this.averageHeight = null;
            this.liveStemVolume = null;
            this.treeBasalArea = null;
            this.treeLai = null;
            this.treeNpp = null;
            this.treeAbovegroundNpp = null;
            this.treesPerHectare = null;
            this.saplingBasalArea = null;
            this.saplingLai = null;
            this.saplingCohorts = null;
            this.saplingMeanAge = null;
            this.saplingNpp = null;
            this.saplingsPerHectare = null;
            this.branchCarbon = null;
            this.branchNitrogen = null;
            this.coarseRootCarbon = null;
            this.coarseRootNitrogen = null;
            this.fineRootCarbon = null;
            this.fineRootNitrogen = null;
            this.foliageCarbon = null;
            this.foliageNitrogen = null;
            this.regenerationCarbon = null;
            this.regenerationNitrogen = null;
            this.stemCarbon = null;
            this.stemNitrogen = null;
        }

        /// <summary>
        /// Append trajectory to record batch.
        /// </summary>
        /// <param name="trajectory">Resource unit all species, resource unit tree species, or stand trajectory to copy into record batch memory.</param>
        /// <param name="polygonID">Resource unit or stand ID.</param>
        /// <param name="treeSpeciesCode">Index of tree species in tree species string table.</param>
        /// <param name="calendarYearSource">Sequential array of calendar years, starting with simulation year zero.</param>
        public void Add(StandOrResourceUnitTrajectory trajectory, UInt32 polygonID, UInt32 treeSpeciesCode, Span<Int16> calendarYearSource)
        {
            (int startIndexInRecordBatch, int yearsToCopyToRecordBatch) = this.GetBatchIndicesForAdd(trajectory.LengthInYears);
            if (startIndexInRecordBatch == 0)
            {
                this.AppendNewBatch();
            }
            this.Add(trajectory, polygonID, treeSpeciesCode, 0, calendarYearSource, startIndexInRecordBatch, yearsToCopyToRecordBatch);

            int yearsRemainingToCopy = trajectory.LengthInYears - yearsToCopyToRecordBatch;
            if (yearsRemainingToCopy > 0)
            {
                this.AppendNewBatch();
                this.Add(trajectory, polygonID, treeSpeciesCode, yearsToCopyToRecordBatch, calendarYearSource, 0, yearsRemainingToCopy);
            }
        }

        private void Add(StandOrResourceUnitTrajectory trajectory, UInt32 polygonID, UInt32 treeSpeciesCode, int startYearIndex, Span<Int16> calendarYearSource, int startIndexInRecordBatch, int yearsToCopy)
        {
            ArrowMemory.Fill(this.id, polygonID, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(calendarYearSource, startYearIndex, this.calendarYear, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.Fill(this.treeSpeciesIndices, this.treeSpeciesFieldType, treeSpeciesCode, startIndexInRecordBatch, yearsToCopy);

            ArrowMemory.CopyN(trajectory.AverageDbhByYear, startYearIndex, this.averageDbh, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.AverageHeightByYear, startYearIndex, this.averageHeight, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.LiveStemVolumeByYear, startYearIndex, this.liveStemVolume, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.TreeBasalAreaByYear, startYearIndex, this.treeBasalArea, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.TreeLeafAreaIndexByYear, startYearIndex, this.treeLai, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.TreeNppByYear, startYearIndex, this.treeNpp, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.TreeNppAbovegroundByYear, startYearIndex, this.treeAbovegroundNpp, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.TreesPerHectareByYear, startYearIndex, this.treesPerHectare, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.SaplingBasalAreaByYear, startYearIndex, this.saplingBasalArea, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.SaplingLeafAreaIndexByYear, startYearIndex, this.saplingLai, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.SaplingCohortsPerHectareByYear, startYearIndex, this.saplingCohorts, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.SaplingMeanAgeByYear, startYearIndex, this.saplingMeanAge, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.SaplingNppByYear, startYearIndex, this.saplingNpp, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.SaplingsPerHectareByYear, startYearIndex, this.saplingsPerHectare, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.BranchCarbonByYear, startYearIndex, this.branchCarbon, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.BranchNitrogenByYear, startYearIndex, this.branchNitrogen, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.CoarseRootCarbonByYear, startYearIndex, this.coarseRootCarbon, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.CoarseRootNitrogenByYear, startYearIndex, this.coarseRootNitrogen, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.FineRootCarbonByYear, startYearIndex, this.fineRootCarbon, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.FineRootNitrogenByYear, startYearIndex, this.fineRootNitrogen, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.FoliageCarbonByYear, startYearIndex, this.foliageCarbon, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.FoliageNitrogenByYear, startYearIndex, this.foliageNitrogen, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.RegenerationCarbonByYear, startYearIndex, this.regenerationCarbon, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.RegenerationNitrogenByYear, startYearIndex, this.regenerationNitrogen, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.StemCarbonByYear, startYearIndex, this.stemCarbon, startIndexInRecordBatch, yearsToCopy);
            ArrowMemory.CopyN(trajectory.StemNitrogenByYear, startYearIndex, this.stemNitrogen, startIndexInRecordBatch, yearsToCopy);

            this.Count += yearsToCopy;
        }

        private void AppendNewBatch()
        {
            int capacityInRecords = this.GetNextBatchLength();

            // 27 fields @ 4 bytes/field -> 9700 trajectory years/MB -> 103 MB for one century of 10,000 resource units' all species trajectories
            // If needed, restricted batch lengths can be supported. But, for now, it's assumed a few hundred MB isn't a concern.
            this.id = new byte[capacityInRecords * sizeof(Int32)];
            this.calendarYear = new byte[capacityInRecords * sizeof(Int16)];
            this.treeSpeciesIndices = new byte[capacityInRecords * treeSpeciesFieldType.BitWidth / 8];
            this.averageDbh = new byte[capacityInRecords * sizeof(float)];
            this.averageHeight = new byte[capacityInRecords * sizeof(float)];
            this.liveStemVolume = new byte[capacityInRecords * sizeof(float)];
            this.treeBasalArea = new byte[capacityInRecords * sizeof(float)];
            this.treeLai = new byte[capacityInRecords * sizeof(float)];
            this.treeNpp = new byte[capacityInRecords * sizeof(float)];
            this.treeAbovegroundNpp = new byte[capacityInRecords * sizeof(float)];
            this.treesPerHectare = new byte[capacityInRecords * sizeof(float)];
            this.saplingBasalArea = new byte[capacityInRecords * sizeof(float)];
            this.saplingLai = new byte[capacityInRecords * sizeof(float)];
            this.saplingCohorts = new byte[capacityInRecords * sizeof(float)];
            this.saplingMeanAge = new byte[capacityInRecords * sizeof(float)];
            this.saplingNpp = new byte[capacityInRecords * sizeof(float)];
            this.saplingsPerHectare = new byte[capacityInRecords * sizeof(float)];
            this.branchCarbon = new byte[capacityInRecords * sizeof(float)];
            this.branchNitrogen = new byte[capacityInRecords * sizeof(float)];
            this.coarseRootCarbon = new byte[capacityInRecords * sizeof(float)];
            this.coarseRootNitrogen = new byte[capacityInRecords * sizeof(float)];
            this.fineRootCarbon = new byte[capacityInRecords * sizeof(float)];
            this.fineRootNitrogen = new byte[capacityInRecords * sizeof(float)];
            this.foliageCarbon = new byte[capacityInRecords * sizeof(float)];
            this.foliageNitrogen = new byte[capacityInRecords * sizeof(float)];
            this.regenerationCarbon = new byte[capacityInRecords * sizeof(float)];
            this.regenerationNitrogen = new byte[capacityInRecords * sizeof(float)];
            this.stemCarbon = new byte[capacityInRecords * sizeof(float)];
            this.stemNitrogen = new byte[capacityInRecords * sizeof(float)];

            // repackage arrays into Arrow record batch
            IArrowArray[] arrowArrays =
            [
                ArrowArrayExtensions.WrapInInt32(this.id),
                ArrowArrayExtensions.WrapInInt16(this.calendarYear),
                // not supported in Arrow 12.0.0
                // ArrowArrayExtensions.BindStringTable256(this.treeSpeciesIndices, treeSpecies),
                ArrowArrayExtensions.Wrap(treeSpeciesFieldType, this.treeSpeciesIndices),
                ArrowArrayExtensions.WrapInFloat(this.averageDbh),
                ArrowArrayExtensions.WrapInFloat(this.averageHeight),
                ArrowArrayExtensions.WrapInFloat(this.liveStemVolume),
                ArrowArrayExtensions.WrapInFloat(this.treeBasalArea),
                ArrowArrayExtensions.WrapInFloat(this.treeLai),
                ArrowArrayExtensions.WrapInFloat(this.treeNpp),
                ArrowArrayExtensions.WrapInFloat(this.treeAbovegroundNpp),
                ArrowArrayExtensions.WrapInFloat(this.treesPerHectare),
                ArrowArrayExtensions.WrapInFloat(this.saplingBasalArea),
                ArrowArrayExtensions.WrapInFloat(this.saplingLai),
                ArrowArrayExtensions.WrapInFloat(this.saplingCohorts),
                ArrowArrayExtensions.WrapInFloat(this.saplingMeanAge),
                ArrowArrayExtensions.WrapInFloat(this.saplingNpp),
                ArrowArrayExtensions.WrapInFloat(this.saplingsPerHectare),
                ArrowArrayExtensions.WrapInFloat(this.branchCarbon),
                ArrowArrayExtensions.WrapInFloat(this.branchNitrogen),
                ArrowArrayExtensions.WrapInFloat(this.coarseRootCarbon),
                ArrowArrayExtensions.WrapInFloat(this.coarseRootNitrogen),
                ArrowArrayExtensions.WrapInFloat(this.fineRootCarbon),
                ArrowArrayExtensions.WrapInFloat(this.fineRootNitrogen),
                ArrowArrayExtensions.WrapInFloat(this.foliageCarbon),
                ArrowArrayExtensions.WrapInFloat(this.foliageNitrogen),
                ArrowArrayExtensions.WrapInFloat(this.regenerationCarbon),
                ArrowArrayExtensions.WrapInFloat(this.regenerationNitrogen),
                ArrowArrayExtensions.WrapInFloat(this.stemCarbon),
                ArrowArrayExtensions.WrapInFloat(this.stemNitrogen)
            ];

            this.RecordBatches.Add(new(this.schema, arrowArrays, capacityInRecords));
        }
    }
}