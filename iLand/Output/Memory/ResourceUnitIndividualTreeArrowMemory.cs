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

        private readonly Schema schema;

        // if needed, a resource unit ID field can be included
        private byte[]? calendarYear;
        private byte[]? standID;
        private byte[]? treeSpeciesIndices;
        private byte[]? treeID;
        private byte[]? dbhInCm;
        private byte[]? heightInM;
        private byte[]? leafAreaInM2;
        private byte[]? lightResourceIndex;
        private byte[]? lightResponse;
        private byte[]? stressIndex;
        private byte[]? nppReserveInKg;
        private byte[]? opacity;
        private byte[]? ageInYears;
        private byte[]? coarseRootMassInKg;
        private byte[]? fineRootMassInKg;
        private byte[]? foliageMassInKg;
        private byte[]? stemMassInKg;

        public ResourceUnitIndividualTreeArrowMemory(IntegerType treeSpeciesFieldType, int capacityInRecords)
            : base(capacityInRecords, ArrowMemory.DefaultMaximumRecordsPerBatch)
        {
            this.treeSpeciesFieldType = treeSpeciesFieldType;

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
            this.schema = new(fields, metadata);

            this.calendarYear = null;
            this.standID = null;
            this.treeSpeciesIndices = null;
            this.treeID = null;
            this.dbhInCm = null;
            this.heightInM = null;
            this.leafAreaInM2 = null;
            this.lightResourceIndex = null;
            this.lightResponse = null;
            this.stressIndex = null;
            this.nppReserveInKg = null;
            this.opacity = null;
            this.ageInYears = null;
            this.coarseRootMassInKg = null;
            this.fineRootMassInKg = null;
            this.foliageMassInKg = null;
            this.stemMassInKg = null;
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

                (int startIndexInRecordBatch, int treesToCopyToRecordBatch) = this.GetBatchIndicesForAdd(treesOfSpecies.Count);
                if (startIndexInRecordBatch == 0)
                {
                    this.AppendNewBatch();
                }
                this.Add(treesOfSpecies, treeSpeciesCode,0,  calendarYear, startIndexInRecordBatch, treesToCopyToRecordBatch);

                int treesRemainingToCopy = treesOfSpecies.Count - treesToCopyToRecordBatch;
                if (treesRemainingToCopy > 0)
                {
                    this.AppendNewBatch();
                    this.Add(treesOfSpecies, treeSpeciesCode, treesToCopyToRecordBatch, calendarYear, 0, treesRemainingToCopy);
                }
            }
        }

        private void Add(TreeListBiometric treesOfSpecies, UInt32 treeSpeciesCode, int startIndexInTreeList, Int16 calendarYear, int startIndexInRecordBatch, int treesToCopy)
        {
            ArrowMemory.Fill(this.calendarYear, calendarYear, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.StandID, startIndexInTreeList, this.standID, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.Fill(this.treeSpeciesIndices, this.treeSpeciesFieldType, treeSpeciesCode, startIndexInRecordBatch, treesToCopy);

            ArrowMemory.CopyN(treesOfSpecies.TreeID, startIndexInTreeList, this.treeID, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.DbhInCm, startIndexInTreeList, this.dbhInCm, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.HeightInM, startIndexInTreeList, this.heightInM, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.LeafAreaInM2, startIndexInTreeList, this.leafAreaInM2, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.LightResourceIndex, startIndexInTreeList, this.lightResourceIndex, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.LightResponse, startIndexInTreeList, this.lightResponse, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.StressIndex, startIndexInTreeList, this.stressIndex, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.NppReserveInKg, startIndexInTreeList, this.nppReserveInKg, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.Opacity, startIndexInTreeList, this.opacity, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.AgeInYears, startIndexInTreeList, this.ageInYears, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.CoarseRootMassInKg, startIndexInTreeList, this.coarseRootMassInKg, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.FineRootMassInKg, startIndexInTreeList, this.fineRootMassInKg, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.FoliageMassInKg, startIndexInTreeList, this.foliageMassInKg, startIndexInRecordBatch, treesToCopy);
            ArrowMemory.CopyN(treesOfSpecies.StemMassInKg, startIndexInTreeList, this.stemMassInKg, startIndexInRecordBatch, treesToCopy);

            this.Count += treesToCopy;
        }

        private void AppendNewBatch()
        {
            int batchLength = this.GetNextBatchLength();

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

            // repackage arrays into Arrow record batch
            IArrowArray[] arrowArrays = new IArrowArray[]
            {
                ArrowArrayExtensions.WrapInInt16(this.calendarYear),
                ArrowArrayExtensions.WrapInInt32(this.standID),
                // not supported in Arrow 12.0
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

            this.RecordBatches.Add(new(this.schema, arrowArrays, batchLength));
        }
    }
}