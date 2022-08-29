using iLand.Simulation;
using iLand.Tool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace iLand.Tree
{
    internal class TreeVariableAccessor : ExpressionVariableAccessor
    {
        private static readonly ReadOnlyCollection<string> TreeVariableNames;

        public int TreeIndex { get; set; }
        public TreeListSpatial? Trees { get; set; }

        static TreeVariableAccessor()
        {
            TreeVariableAccessor.TreeVariableNames = new List<string>(ExpressionVariableAccessor.BaseVariableNames)
            {
                "id", "dbh", "height", "resourceUnit", "x", "y", "volume", "lri", "leafarea", "lightresponse", // fields 0-9
                "woodymass", "rootmass", "foliagemass", "age", "opacity" /* 10-14 */, "dead", "stress", "deltad", // 15-17
                "afoliagemass", "species", "basalarea", "crownarea" /* 20, 21 */, "markharvest", "markcut", "markcrop", "markcompetitor" // 18-25
            }.AsReadOnly();
        }

        public TreeVariableAccessor(SimulationState? simulationState)
            : base(simulationState, null)
        {
            this.Trees = null;
            this.TreeIndex = -1;
        }

        //public TreeWrapper(Trees trees)
        //{
        //    this.Trees = trees;
        //    this.TreeIndex = -1;
        //}

        public override ReadOnlyCollection<string> GetVariableNames()
        {
            return TreeVariableAccessor.TreeVariableNames;
        }

        public override float GetValue(int variableIndex)
        {
            Debug.Assert(this.Trees != null);

            switch (variableIndex - ExpressionVariableAccessor.BaseVariableNames.Count)
            {
                case 0: 
                    return this.Trees.TreeID[this.TreeIndex];
                case 1: 
                    return this.Trees.DbhInCm[this.TreeIndex];
                case 2: 
                    return this.Trees.HeightInM[this.TreeIndex];
                case 3: 
                    return this.Trees.ResourceUnit.ID;
                case 4: 
                    return this.Trees.LightCellIndexXY[this.TreeIndex].X;
                case 5: 
                    return this.Trees.LightCellIndexXY[this.TreeIndex].Y;
                case 6: 
                    return this.Trees.GetStemVolume(this.TreeIndex);
                case 7: 
                    return this.Trees.LightResourceIndex[this.TreeIndex];
                case 8: 
                    return this.Trees.LeafAreaInM2[this.TreeIndex];
                case 9: 
                    return this.Trees.LightResponse[this.TreeIndex];
                case 10: 
                    return this.Trees.StemMassInKg[this.TreeIndex];
                case 11: 
                    return this.Trees.CoarseRootMassInKg[this.TreeIndex] + this.Trees.FineRootMassInKg[this.TreeIndex];
                case 12: 
                    return this.Trees.FoliageMassInKg[this.TreeIndex];
                case 13: 
                    return this.Trees.AgeInYears[this.TreeIndex];
                case 14: 
                    return this.Trees.Opacity[this.TreeIndex];
                case 15: 
                    return this.Trees.IsDead(this.TreeIndex) ? 1.0F : 0.0F;
                case 16: 
                    return this.Trees.StressIndex[this.TreeIndex];
                case 17: 
                    return this.Trees.DbhDeltaInCm[this.TreeIndex]; // diameter increment of most recent year simulated
                case 18: 
                    return this.Trees.Species.GetBiomassFoliage(this.Trees.DbhInCm[this.TreeIndex]); 
                case 19: 
                    return this.Trees.Species.Index;
                case 20: 
                    return this.Trees.GetBasalArea(this.TreeIndex);
                case 21: 
                    float crownRadiusInM = this.Trees.GetCrownRadius(this.TreeIndex);
                    return MathF.PI * crownRadiusInM * crownRadiusInM; // area (m²) of the crown
                case 22: 
                    return this.Trees.IsMarkedForHarvest(this.TreeIndex) ? 1 : 0;
                case 23: 
                    return this.Trees.IsMarkedForCut(this.TreeIndex) ? 1 : 0;
                case 24: 
                    return this.Trees.IsMarkedAsCropTree(this.TreeIndex) ? 1 : 0;
                case 25: 
                    return this.Trees.IsMarkedAsCropCompetitor(this.TreeIndex) ? 1 : 0;
                default:
                    return base.GetValue(variableIndex);
            };
        }
    }
}
