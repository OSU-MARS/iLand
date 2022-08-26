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
                "id", "dbh", "height", "ruindex", "x", "y", "volume", "lri", "leafarea", "lightresponse", // fields 0-9
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

            return (variableIndex - ExpressionVariableAccessor.BaseVariableNames.Count) switch
            {
                0 => this.Trees.TreeID[this.TreeIndex],// id
                1 => this.Trees.DbhInCm[this.TreeIndex],// dbh
                2 => this.Trees.HeightInM[this.TreeIndex],// height
                3 => this.Trees.ResourceUnit.ResourceUnitGridIndex,// ruindex
                4 => this.Trees.GetCellCenterPoint(this.TreeIndex).X,// x
                5 => this.Trees.GetCellCenterPoint(this.TreeIndex).Y,// y
                6 => this.Trees.GetStemVolume(this.TreeIndex),// volume
                7 => this.Trees.LightResourceIndex[this.TreeIndex],// lri
                8 => this.Trees.LeafAreaInM2[this.TreeIndex],
                9 => this.Trees.LightResponse[this.TreeIndex],
                10 => this.Trees.StemMassInKg[this.TreeIndex],
                11 => this.Trees.CoarseRootMassInKg[this.TreeIndex] + this.Trees.FineRootMassInKg[this.TreeIndex],// sum of coarse and fine roots
                12 => this.Trees.FoliageMassInKg[this.TreeIndex],
                13 => this.Trees.AgeInYears[this.TreeIndex],
                14 => this.Trees.Opacity[this.TreeIndex],
                15 => this.Trees.IsDead(this.TreeIndex) ? 1.0F : 0.0F,
                16 => this.Trees.StressIndex[this.TreeIndex],
                17 => this.Trees.DbhDeltaInCm[this.TreeIndex],// increment of last year
                18 => this.Trees.Species.GetBiomassFoliage(this.Trees.DbhInCm[this.TreeIndex]),// allometric foliage
                19 => this.Trees.Species.Index,
                20 => this.Trees.GetBasalArea(this.TreeIndex),
                21 => this.Trees.GetCrownRadius(this.TreeIndex) * this.Trees.GetCrownRadius(this.TreeIndex) * MathF.PI,// area (m2) of the crown
                22 => this.Trees.IsMarkedForHarvest(this.TreeIndex) ? 1 : 0,// markharvest
                23 => this.Trees.IsMarkedForCut(this.TreeIndex) ? 1 : 0,// markcut
                24 => this.Trees.IsMarkedAsCropTree(this.TreeIndex) ? 1 : 0,// markcrop
                25 => this.Trees.IsMarkedAsCropCompetitor(this.TreeIndex) ? 1 : 0,// markcompetitor
                _ => base.GetValue(variableIndex),
            };
        }
    }
}
