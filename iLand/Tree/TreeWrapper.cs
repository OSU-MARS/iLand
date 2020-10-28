using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace iLand.Tree
{
    internal class TreeWrapper : ExpressionWrapper
    {
        private static readonly ReadOnlyCollection<string> TreeVariableNames;

        public int TreeIndex { get; set; }
        public Trees Trees { get; set; }

        static TreeWrapper()
        {
            TreeWrapper.TreeVariableNames = new List<string>(BaseVariableNames)
            {
                "id", "dbh", "height", "ruindex" /* 0..3*/, "x", "y", "volume", "lri", "leafarea", "lightresponse", // 4-9
                "woodymass", "rootmass", "foliagemass", "age", "opacity" /* 10-14 */, "dead", "stress", "deltad", //15-17
                "afoliagemass", "species" /* 18, 19 */, "basalarea", "crownarea" /* 20, 21 */, "markharvest", "markcut", "markcrop", "markcompetitor"
            }.AsReadOnly(); // 22-25
        }

        public TreeWrapper()
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
            return TreeWrapper.TreeVariableNames;
        }

        public override double GetValue(Simulation.Model model, int variableIndex)
        {
            Debug.Assert(this.Trees != null);

            return (variableIndex - BaseVariableNames.Count) switch
            {
                0 => this.Trees.ID[this.TreeIndex],// id
                1 => this.Trees.Dbh[this.TreeIndex],// dbh
                2 => this.Trees.Height[this.TreeIndex],// height
                3 => (double)this.Trees.RU.GridIndex,// ruindex
                4 => this.Trees.GetCellCenterPoint(this.TreeIndex).X,// x
                5 => this.Trees.GetCellCenterPoint(this.TreeIndex).Y,// y
                6 => this.Trees.GetStemVolume(this.TreeIndex),// volume
                7 => this.Trees.LightResourceIndex[this.TreeIndex],// lri
                8 => this.Trees.LeafArea[this.TreeIndex],
                9 => this.Trees.LightResponse[this.TreeIndex],
                10 => this.Trees.StemMass[this.TreeIndex],
                11 => this.Trees.CoarseRootMass[this.TreeIndex] + this.Trees.FineRootMass[this.TreeIndex],// sum of coarse and fine roots
                12 => this.Trees.FoliageMass[this.TreeIndex],
                13 => this.Trees.Age[this.TreeIndex],
                14 => this.Trees.Opacity[this.TreeIndex],
                15 => this.Trees.IsDead(this.TreeIndex) ? 1.0 : 0.0,
                16 => this.Trees.StressIndex[this.TreeIndex],
                17 => this.Trees.DbhDelta[this.TreeIndex],// increment of last year
                18 => this.Trees.Species.GetBiomassFoliage(this.Trees.Dbh[this.TreeIndex]),// allometric foliage
                19 => this.Trees.Species.Index,
                20 => this.Trees.GetBasalArea(this.TreeIndex),
                21 => this.Trees.GetCrownRadius(this.TreeIndex) * this.Trees.GetCrownRadius(this.TreeIndex) * MathF.PI,// area (m2) of the crown
                22 => this.Trees.IsMarkedForHarvest(this.TreeIndex) ? 1 : 0,// markharvest
                23 => this.Trees.IsMarkedForCut(this.TreeIndex) ? 1 : 0,// markcut
                24 => this.Trees.IsMarkedAsCropTree(this.TreeIndex) ? 1 : 0,// markcrop
                25 => this.Trees.IsMarkedAsCropCompetitor(this.TreeIndex) ? 1 : 0,// markcompetitor
                _ => base.GetValue(model, variableIndex),
            };
        }
    }
}
