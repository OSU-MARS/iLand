﻿using iLand.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Tools
{
    internal class TreeWrapper : ExpressionWrapper
    {
        private static readonly List<string> treeVarList;

        public Tree Tree { get; set; }

        static TreeWrapper()
        {
            treeVarList = new List<string>(BaseVarList);
            treeVarList.AddRange(new string[] { "id", "dbh", "height", "ruindex" /* 0..3*/, "x", "y", "volume", "lri", "leafarea", "lightresponse", // 4-9
                               "woodymass", "rootmass", "foliagemass", "age", "opacity" /* 10-14 */, "dead", "stress", "deltad", //15-17
                               "afoliagemass", "species" /* 18, 19 */, "basalarea", "crownarea" /* 20, 21 */, "markharvest", "markcut", "markcrop", "markcompetitor" }); // 22-25
        }

        public TreeWrapper()
        {
            Tree = null;
        }

        public TreeWrapper(Tree tree)
        {
            Tree = tree;
        }

        public override List<string> GetVariablesList()
        {
            return treeVarList;
        }

        public override double Value(int variableIndex)
        {
            Debug.Assert(Tree != null);

            return (variableIndex - BaseVarList.Count) switch
            {
                0 => Tree.ID,// id
                1 => Tree.Dbh,// dbh
                2 => Tree.Height,// height
                3 => (double)Tree.RU.Index,// ruindex
                4 => Tree.GetCellCenterPoint().X,// x
                5 => Tree.GetCellCenterPoint().Y,// y
                6 => Tree.Volume(),// volume
                7 => Tree.LightResourceIndex,// lri
                8 => Tree.LeafArea,
                9 => Tree.mLightResponse,
                10 => Tree.StemMass,
                11 => Tree.CoarseRootMass + Tree.FineRootMass,// sum of coarse and fine roots
                12 => Tree.FoliageMass,
                13 => Tree.Age,
                14 => Tree.mOpacity,
                15 => Tree.IsDead() ? 1.0 : 0.0,
                16 => Tree.StressIndex,
                17 => Tree.mDbhDelta,// increment of last year
                18 => Tree.Species.GetBiomassFoliage(Tree.Dbh),// allometric foliage
                19 => Tree.Species.Index,
                20 => Tree.BasalArea(),
                21 => Tree.GetCrownRadius() * Tree.GetCrownRadius() * Math.PI,// area (m2) of the crown
                22 => Tree.IsMarkedForHarvest() ? 1 : 0,// markharvest
                23 => Tree.IsMarkedForCut() ? 1 : 0,// markcut
                24 => Tree.IsMarkedAsCropTree() ? 1 : 0,// markcrop
                25 => Tree.IsMarkedAsCropCompetitor() ? 1 : 0,// markcompetitor
                _ => base.Value(variableIndex),
            };
        }
    }
}
