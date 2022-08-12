using Apache.Arrow;
using Apache.Arrow.Types;
using iLand.Extensions;
using System;
using System.Collections.Generic;

namespace iLand.Output.Memory
{
    internal class StandOrResourceUnitTrajectoryArrowMemory : ArrowMemory
    {
        private readonly IntegerType treeSpeciesFieldType;

        private readonly byte[] id;
        private readonly byte[] calendarYear;
        private readonly byte[] treeSpeciesIndices;
        private readonly byte[] averageDbh;
        private readonly byte[] averageHeight;
        private readonly byte[] basalArea;
        private readonly byte[] lai;
        private readonly byte[] liveStemVolume;
        private readonly byte[] treeNpp;
        private readonly byte[] treeAbovegroundNpp;
        private readonly byte[] treesPerHectare;
        private readonly byte[] saplingCohorts;
        private readonly byte[] saplingMeanAge;
        private readonly byte[] saplingNpp;
        private readonly byte[] saplingsPerHectare;
        private readonly byte[] branchCarbon;
        private readonly byte[] branchNitrogen;
        private readonly byte[] coarseRootCarbon;
        private readonly byte[] coarseRootNitrogen;
        private readonly byte[] fineRootCarbon;
        private readonly byte[] fineRootNitrogen;
        private readonly byte[] foliageCarbon;
        private readonly byte[] foliageNitrogen;
        private readonly byte[] regenerationCarbon;
        private readonly byte[] regenerationNitrogen;
        private readonly byte[] stemCarbon;
        private readonly byte[] stemNitrogen;

        public RecordBatch RecordBatch { get; private init; }

        // public StandOrResourceUnitTrajectoryArrowMemory(string idFieldName, IList<string> treeSpecies, int batchLength)
        public StandOrResourceUnitTrajectoryArrowMemory(string idFieldName, IntegerType treeSpeciesFieldType, int batchLength)
        {
            this.treeSpeciesFieldType = treeSpeciesFieldType;

            // 27 fields @ 4 bytes/field -> 9700 trajectory years/MB -> 103 MB for one century of 10,000 resource units' all species trajectories
            // If needed, restricted batch lengths can be supported. But, for now, it's assumed a few hundred MB isn't a concern.
            this.id = new byte[batchLength * sizeof(Int32)];
            this.calendarYear = new byte[batchLength * sizeof(Int32)];
            this.treeSpeciesIndices = new byte[batchLength * treeSpeciesFieldType.BitWidth / 8];
            this.averageDbh = new byte[batchLength * sizeof(float)];
            this.averageHeight = new byte[batchLength * sizeof(float)];
            this.basalArea = new byte[batchLength * sizeof(float)];
            this.lai = new byte[batchLength * sizeof(float)];
            this.liveStemVolume = new byte[batchLength * sizeof(float)];
            this.treeNpp = new byte[batchLength * sizeof(float)];
            this.treeAbovegroundNpp = new byte[batchLength * sizeof(float)];
            this.treesPerHectare = new byte[batchLength * sizeof(float)];
            this.saplingCohorts = new byte[batchLength * sizeof(float)];
            this.saplingMeanAge = new byte[batchLength * sizeof(float)];
            this.saplingNpp = new byte[batchLength * sizeof(float)];
            this.saplingsPerHectare = new byte[batchLength * sizeof(float)];
            this.branchCarbon = new byte[batchLength * sizeof(float)];
            this.branchNitrogen = new byte[batchLength * sizeof(float)];
            this.coarseRootCarbon = new byte[batchLength * sizeof(float)];
            this.coarseRootNitrogen = new byte[batchLength * sizeof(float)];
            this.fineRootCarbon = new byte[batchLength * sizeof(float)];
            this.fineRootNitrogen = new byte[batchLength * sizeof(float)];
            this.foliageCarbon = new byte[batchLength * sizeof(float)];
            this.foliageNitrogen = new byte[batchLength * sizeof(float)];
            this.regenerationCarbon = new byte[batchLength * sizeof(float)];
            this.regenerationNitrogen = new byte[batchLength * sizeof(float)];
            this.stemCarbon = new byte[batchLength * sizeof(float)];
            this.stemNitrogen = new byte[batchLength * sizeof(float)];

            // create schema
            List<Field> fields = new()
            {
                new(idFieldName, Int32Type.Default, false),
                new("year", Int32Type.Default, false),
                new("species", treeSpeciesFieldType, false),
                new("averageDbh", FloatType.Default, false),
                new("averageHeight", FloatType.Default, false),
                new("basalArea", FloatType.Default, false),
                new("lai", FloatType.Default, false),
                new("liveStemVolume", FloatType.Default, false),
                new("treeNpp", FloatType.Default, false),
                new("treeAbovegroundNpp", FloatType.Default, false),
                new("treesPerHectare", FloatType.Default, false),
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
            };

            Dictionary<string, string> metadata = new();
            Schema schema = new(fields, metadata);

            // repackage arrays into Arrow record batch
            IArrowArray[] arrowArrays = new IArrowArray[]
            {
                ArrowArrayExtensions.WrapInInt32(this.id),
                ArrowArrayExtensions.WrapInInt32(this.calendarYear),
                // not supported in Apache 9.0.0
                // ArrowArrayExtensions.BindStringTable256(this.treeSpeciesIndices, treeSpecies),
                ArrowArrayExtensions.Wrap(treeSpeciesFieldType, this.treeSpeciesIndices),
                ArrowArrayExtensions.WrapInFloat(this.averageDbh),
                ArrowArrayExtensions.WrapInFloat(this.averageHeight),
                ArrowArrayExtensions.WrapInFloat(this.basalArea),
                ArrowArrayExtensions.WrapInFloat(this.lai),
                ArrowArrayExtensions.WrapInFloat(this.liveStemVolume),
                ArrowArrayExtensions.WrapInFloat(this.treeNpp),
                ArrowArrayExtensions.WrapInFloat(this.treeAbovegroundNpp),
                ArrowArrayExtensions.WrapInFloat(this.treesPerHectare),
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
                ArrowArrayExtensions.WrapInFloat(this.stemNitrogen),
            };

            this.RecordBatch = new(schema, arrowArrays, batchLength);
        }

        /// <summary>
        /// Append trajectory to record batch.
        /// </summary>
        /// <param name="trajectory">Resource unit all species, resource unit tree species, or stand trajectory to copy into record batch memory.</param>
        /// <param name="polygonID">Resource unit or stand ID.</param>
        /// <param name="treeSpeciesCode">Index of tree species in tree species string table.</param>
        /// <param name="calendarYearSource">Sequential array of calendar years, starting with simulation year zero.</param>
        public void Add(StandOrResourceUnitTrajectory trajectory, int polygonID, int treeSpeciesCode, Span<int> calendarYearSource)
        {
            int trajectoryLengthInYears = trajectory.LengthInYears;

            this.Fill(this.id, polygonID, trajectoryLengthInYears);
            this.CopyFirstN(calendarYearSource, this.calendarYear, trajectoryLengthInYears);
            this.Fill(this.treeSpeciesIndices, this.treeSpeciesFieldType, treeSpeciesCode, trajectoryLengthInYears);

            this.CopyFirstN(trajectory.AverageDbhByYear, this.averageDbh, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.AverageHeightByYear, this.averageHeight, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.BasalAreaByYear, this.basalArea, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.LeafAreaIndexByYear, this.lai, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.LiveStemVolumeByYear, this.liveStemVolume, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.TreeNppByYear, this.treeNpp, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.TreeNppAbovegroundByYear, this.treeAbovegroundNpp, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.TreesPerHectareByYear, this.treesPerHectare, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.SaplingCohortsPerHectareByYear, this.saplingCohorts, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.SaplingMeanAgeByYear, this.saplingMeanAge, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.SaplingNppByYear, this.saplingNpp, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.SaplingsPerHectareByYear, this.saplingsPerHectare, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.BranchCarbonByYear, this.branchCarbon, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.BranchNitrogenByYear, this.branchNitrogen, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.CoarseRootCarbonByYear, this.coarseRootCarbon, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.CoarseRootNitrogenByYear, this.coarseRootNitrogen, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.FineRootCarbonByYear, this.fineRootCarbon, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.FineRootNitrogenByYear, this.fineRootNitrogen, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.FoliageCarbonByYear, this.foliageCarbon, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.FoliageNitrogenByYear, this.foliageNitrogen, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.RegenerationCarbonByYear, this.regenerationCarbon, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.RegenerationNitrogenByYear, this.regenerationNitrogen, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.StemCarbonByYear, this.stemCarbon, trajectoryLengthInYears);
            this.CopyFirstN(trajectory.StemNitrogenByYear, this.stemNitrogen, trajectoryLengthInYears);

            this.Count += trajectoryLengthInYears;
        }
    }
}
