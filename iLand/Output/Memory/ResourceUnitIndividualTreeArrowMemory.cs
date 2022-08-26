using Apache.Arrow;
using Apache.Arrow.Types;
using iLand.Extensions;
using iLand.Tree;
using System;
using System.Collections.Generic;

namespace iLand.Output.Memory
{
    internal class ResourceUnitIndividualTreeArrowMemory : ArrowMemory
    {
        private readonly IntegerType treeSpeciesFieldType;

        // if needed, a resource unit ID field can be included
        private readonly byte[] calendarYear;
        private readonly byte[] standID;
        private readonly byte[] treeSpeciesIndices;
        private readonly byte[] treeID;
        private readonly byte[] dbhInCm;
        private readonly byte[] heightInM;
        private readonly byte[] leafAreaInM2;
        private readonly byte[] lightResourceIndex;
        private readonly byte[] lightResponse;
        private readonly byte[] stressIndex;
        private readonly byte[] nppReserveInKg;
        private readonly byte[] opacity;
        private readonly byte[] ageInYears;
        private readonly byte[] coarseRootMassInKg;
        private readonly byte[] fineRootMassInKg;
        private readonly byte[] foliageMassInKg;
        private readonly byte[] stemMassInKg;

        public RecordBatch RecordBatch { get; private init; }

        public ResourceUnitIndividualTreeArrowMemory(IntegerType treeSpeciesFieldType, int batchLength)
        {
            this.treeSpeciesFieldType = treeSpeciesFieldType;

            this.calendarYear = new byte[batchLength * sizeof(Int16)];
            this.standID = new byte[batchLength * sizeof(Int32)];
            this.treeSpeciesIndices = new byte[batchLength * treeSpeciesFieldType.BitWidth / 8];
            this.treeID = new byte[batchLength * sizeof(Int32)];
            this.dbhInCm = new byte[batchLength * sizeof(float)];
            this.heightInM = new byte[batchLength * sizeof(float)];
            this.leafAreaInM2 = new byte[batchLength * sizeof(float)];
            this.lightResourceIndex = new byte[batchLength * sizeof(float)];
            this.lightResponse = new byte[batchLength * sizeof(float)];
            this.stressIndex = new byte[batchLength * sizeof(float)];
            this.nppReserveInKg = new byte[batchLength * sizeof(float)];
            this.opacity = new byte[batchLength * sizeof(float)];
            this.ageInYears = new byte[batchLength * sizeof(UInt16)];
            this.coarseRootMassInKg = new byte[batchLength * sizeof(float)];
            this.fineRootMassInKg = new byte[batchLength * sizeof(float)];
            this.foliageMassInKg = new byte[batchLength * sizeof(float)];
            this.stemMassInKg = new byte[batchLength * sizeof(float)];

            // create schema
            List<Field> fields = new()
            {
                new("year", Int16Type.Default, false),
                new("standID", Int32Type.Default, false),
                new("species", treeSpeciesFieldType, false),
                new("id", Int32Type.Default, false),
                new("dbh", FloatType.Default, false),
                new("height", FloatType.Default, false),
                new("leafArea", FloatType.Default, false),
                new("lightResourceIndex", FloatType.Default, false),
                new("lightResponse", FloatType.Default, false),
                new("stressIndex", FloatType.Default, false),
                new("nppReserve", FloatType.Default, false),
                new("opacity", FloatType.Default, false),
                new("age", UInt16Type.Default, false),
                new("coarseRootMass", FloatType.Default, false),
                new("fineRootMass", FloatType.Default, false),
                new("foliageMass", FloatType.Default, false),
                new("stemMass", FloatType.Default, false)
            };

            Dictionary<string, string> metadata = new()
            {
                { "year", "Calendar year." },
                { "standID", "ID number of stand tree is assigned to." },
                { "species", "Integer code for tree species, typically either a USFS FIA code (US Forest Service Forest Inventory and Analysis, 16 bit) or WFO ID (World Flora Online identifier, 32 bit)." },
                { "id", "Tree's tag number or other unique identifier." },
                { "dbh", "Diameter of tree, cm." },
                { "height", "Height of tree, m." },
                { "leafArea", "Leaf area of tree, m²." },
                { "lightResourceIndex", "Tree's light resource index." },
                { "lightResponse", "Tree's light response" },
                { "stressIndex", "Tree's stress index." },
                { "nppReserve", "Tree's stored reserves, kg biomass." },
                { "opacity", "" },
                { "age", "Tree's age in years." },
                { "coarseRootMass", "Mass of tree's coarse roots, kg." },
                { "fineRootMass", "Mass of tree's fine roots, kg." },
                { "foliageMass", "Mass of tree's foliage, kg." },
                { "stemMass", "Mass of tree's stem, kg." }
            };
            Schema schema = new(fields, metadata);

            // repackage arrays into Arrow record batch
            IArrowArray[] arrowArrays = new IArrowArray[]
            {
                ArrowArrayExtensions.WrapInInt16(this.calendarYear),
                ArrowArrayExtensions.WrapInInt32(this.standID),
                // not supported in Apache 9.0.0
                // ArrowArrayExtensions.BindStringTable256(this.treeSpeciesIndices, treeSpecies),
                ArrowArrayExtensions.Wrap(treeSpeciesFieldType, this.treeSpeciesIndices),
                ArrowArrayExtensions.WrapInInt32(this.treeID),
                ArrowArrayExtensions.WrapInFloat(this.dbhInCm),
                ArrowArrayExtensions.WrapInFloat(this.heightInM),
                ArrowArrayExtensions.WrapInFloat(this.leafAreaInM2),
                ArrowArrayExtensions.WrapInFloat(this.lightResourceIndex),
                ArrowArrayExtensions.WrapInFloat(this.lightResponse),
                ArrowArrayExtensions.WrapInFloat(this.stressIndex),
                ArrowArrayExtensions.WrapInFloat(this.nppReserveInKg),
                ArrowArrayExtensions.WrapInFloat(this.opacity),
                ArrowArrayExtensions.WrapInUInt16(this.ageInYears),
                ArrowArrayExtensions.WrapInFloat(this.coarseRootMassInKg),
                ArrowArrayExtensions.WrapInFloat(this.fineRootMassInKg),
                ArrowArrayExtensions.WrapInFloat(this.foliageMassInKg),
                ArrowArrayExtensions.WrapInFloat(this.stemMassInKg)
            };

            this.RecordBatch = new(schema, arrowArrays, batchLength);
        }

        public void Add(ResourceUnitIndividualTreeTrajectories trajectories, UInt32 treeSpeciesCode, int calendarYearBeforeFirstSimulationTimestep)
        {
            Int16 calendarYear = (Int16)calendarYearBeforeFirstSimulationTimestep;
            for (int simulationYear = 0; simulationYear < trajectories.TreesByYear.Length; ++calendarYear, ++simulationYear)
            {
                TreeListBiometric? treesOfSpecies = trajectories.TreesByYear[simulationYear];
                if (treesOfSpecies == null)
                {
                    break;
                }
                int trees = treesOfSpecies.Count;

                this.Fill(this.calendarYear, calendarYear, trees);
                this.CopyFirstN(treesOfSpecies.StandID, this.standID, trees);
                this.Fill(this.treeSpeciesIndices, this.treeSpeciesFieldType, treeSpeciesCode, trees);

                this.CopyFirstN(treesOfSpecies.TreeID, this.treeID, trees);
                this.CopyFirstN(treesOfSpecies.DbhInCm, this.dbhInCm, trees);
                this.CopyFirstN(treesOfSpecies.HeightInM, this.heightInM, trees);
                this.CopyFirstN(treesOfSpecies.LeafAreaInM2, this.leafAreaInM2, trees);
                this.CopyFirstN(treesOfSpecies.LightResourceIndex, this.lightResourceIndex, trees);
                this.CopyFirstN(treesOfSpecies.LightResponse, this.lightResponse, trees);
                this.CopyFirstN(treesOfSpecies.StressIndex, this.stressIndex, trees);
                this.CopyFirstN(treesOfSpecies.NppReserveInKg, this.nppReserveInKg, trees);
                this.CopyFirstN(treesOfSpecies.Opacity, this.opacity, trees);
                this.CopyFirstN(treesOfSpecies.AgeInYears, this.ageInYears, trees);
                this.CopyFirstN(treesOfSpecies.CoarseRootMassInKg, this.coarseRootMassInKg, trees);
                this.CopyFirstN(treesOfSpecies.FineRootMassInKg, this.fineRootMassInKg, trees);
                this.CopyFirstN(treesOfSpecies.FoliageMassInKg, this.foliageMassInKg, trees);
                this.CopyFirstN(treesOfSpecies.StemMassInKg, this.stemMassInKg, trees);

                this.Count += trees;
            }
        }
    }
}