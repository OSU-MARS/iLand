using iLand.core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.tools
{
    internal class TreeWrapper : ExpressionWrapper
    {
        private static readonly List<string> treeVarList;

        private Tree mTree;
        public void setTree(Tree tree) { mTree = tree; }

        static TreeWrapper()
        {
            treeVarList = new List<string>(baseVarList);
            treeVarList.AddRange(new string[] { "id", "dbh", "height", "ruindex" /* 0..3*/, "x", "y", "volume", "lri", "leafarea", "lightresponse", // 4-9
                               "woodymass", "rootmass", "foliagemass", "age", "opacity" /* 10-14 */, "dead", "stress", "deltad", //15-17
                               "afoliagemass", "species" /* 18, 19 */, "basalarea", "crownarea" /* 20, 21 */, "markharvest", "markcut", "markcrop", "markcompetitor" }); // 22-25
        }

        public TreeWrapper()
        {
            mTree = null;
        }

        public TreeWrapper(Tree tree)
        {
            mTree = tree;
        }

        public override List<string> getVariablesList()
        {
            return treeVarList;
        }

        public override double value(int variableIndex)
        {
            Debug.Assert(mTree != null);

            switch (variableIndex - baseVarList.Count)
            {
                case 0: return mTree.id(); // id
                case 1: return mTree.dbh(); // dbh
                case 2: return mTree.height(); // height
                case 3: return (double)mTree.ru().index(); // ruindex
                case 4: return mTree.position().X; // x
                case 5: return mTree.position().Y; // y
                case 6: return mTree.volume(); // volume
                case 7: return mTree.lightResourceIndex(); // lri
                case 8: return mTree.mLeafArea;
                case 9: return mTree.mLightResponse;
                case 10: return mTree.mWoodyMass;
                case 11: return mTree.mCoarseRootMass + mTree.mFineRootMass; // sum of coarse and fine roots
                case 12: return mTree.mFoliageMass;
                case 13: return mTree.age();
                case 14: return mTree.mOpacity;
                case 15: return mTree.isDead() ? 1.0: 0.0;
                case 16: return mTree.mStressIndex;
                case 17: return mTree.mDbhDelta; // increment of last year
                case 18: return mTree.species().biomassFoliage(mTree.dbh()); // allometric foliage
                case 19: return mTree.species().index();
                case 20: return mTree.basalArea();
                case 21: return mTree.crownRadius() * mTree.crownRadius() * Math.PI; // area (m2) of the crown
                case 22: return mTree.isMarkedForHarvest() ? 1 : 0; // markharvest
                case 23: return mTree.isMarkedForCut() ? 1 : 0; // markcut
                case 24: return mTree.isMarkedAsCropTree() ? 1 : 0; // markcrop
                case 25: return mTree.isMarkedAsCropCompetitor() ? 1 : 0; // markcompetitor
                default: return base.value(variableIndex);
            }
        }
    }
}
