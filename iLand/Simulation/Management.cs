﻿using iLand.Tools;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace iLand.Simulation
{
    /** @class Management Management executes management routines.
        The actual iLand management is based on Javascript functions. This class provides
        the frame for executing the javascript as well as the functions that are called by scripts and
        that really do the work.
        See http://iland.boku.ac.at/iLand+scripting, http://iland.boku.ac.at/Object+Management for management Javascript API.
        */
    public class Management
    {
        private List<MutableTuple<Trees, List<int>>> mTreesInMostRecentlyLoadedStand;

        // property getter & setter for removal fractions
        /// removal fraction foliage: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        private readonly float mRemoveFoliage;
        /// removal fraction branch biomass: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        private readonly float mRemoveBranch;
        /// removal float stem biomass: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        private readonly float mRemoveStem;

        public Management()
        {
            // default values for removal fractions
            // 100% of the stem, 0% of foliage and branches
            this.mRemoveFoliage = 0.0F;
            this.mRemoveBranch = 0.0F;
            this.mRemoveStem = 1.0F;
            this.mTreesInMostRecentlyLoadedStand = null;
        }

        // return number of trees currently in list
        public int Count() { return mTreesInMostRecentlyLoadedStand.Count; }
        /// calculate the mean value for all trees in the internal list for 'expression' (filtered by the filter criterion)
        // public double Mean(string expression, string filter = null) { return AggregateFunction(expression, filter, "mean"); }
        /// calculate the sum for all trees in the internal list for the 'expression' (filtered by the filter criterion)
        // public double Sum(string expression, string filter = null) { return AggregateFunction(expression, filter, "sum"); }

        // load all trees, return number of trees
        //public int LoadAll()
        //{ 
        //    return Load(null);
        //}

        public int KillTreesAboveRetentionThreshold(Model model, int treesToRetain)
        {
            AllTreesEnumerator allTreeEnumerator = new AllTreesEnumerator(model);
            List<MutableTuple<Trees, int>> livingTrees = new List<MutableTuple<Trees, int>>();
            while (allTreeEnumerator.MoveNextLiving())
            {
                livingTrees.Add(new MutableTuple<Trees, int>(allTreeEnumerator.CurrentTrees, allTreeEnumerator.CurrentTreeIndex));
            }
            int treesToKill = livingTrees.Count - treesToRetain;
            Debug.WriteLine(livingTrees + " standing, targetsize " + treesToRetain + ", hence " + treesToKill + " trees to remove");
            for (int treesKilled = 0; treesKilled < treesToKill; treesKilled++)
            {
                // TODO: change from O(all trees in model) scaling to O(trees to kill) with data structure for more efficient removal?
                int killIndex = model.RandomGenerator.Random(0, livingTrees.Count);
                livingTrees[killIndex].Item1.Remove(model, livingTrees[killIndex].Item2);
                livingTrees.RemoveAt(killIndex);
            }
            return treesToKill;
        }

        public int KillAllInCurrentStand(Model model, bool removeBiomassFractions)
        {
            int initialTreeCount = mTreesInMostRecentlyLoadedStand.Count;
            foreach (MutableTuple<Trees, List<int>> treesOfSpecies in this.mTreesInMostRecentlyLoadedStand)
            {
                // BUGBUG: doesn't check IsCutDown() flag?
                Trees trees = treesOfSpecies.Item1;
                foreach (int treeIndex in treesOfSpecies.Item2)
                {
                    if (removeBiomassFractions)
                    {
                        trees.Remove(model, treeIndex, this.mRemoveFoliage, this.mRemoveBranch, this.mRemoveStem);
                    }
                    else
                    {
                        trees.Remove(model, treeIndex);
                    }
                }
            }
            mTreesInMostRecentlyLoadedStand.Clear();
            return initialTreeCount;
        }

        public int LethalDisturbanceInCurrentStand(Model model)
        {
            int treeCount = 0;
            foreach (MutableTuple<Trees, List<int>> treesOfSpecies in this.mTreesInMostRecentlyLoadedStand)
            {
                Trees trees = treesOfSpecies.Item1;
                foreach (int treeIndex in treesOfSpecies.Item2)
                {
                    trees.RemoveDisturbance(model, treeIndex, 0.1F, 0.1F, 0.1F, 0.1F, 1.0F);
                    ++treeCount;
                }
            }
            mTreesInMostRecentlyLoadedStand.Clear();
            return treeCount;
        }

        public int Kill(Model model, string filter, double fraction)
        {
            return RemoveTrees(model, filter, fraction, false);
        }

        public int Manage(Model model, string filter, double fraction)
        {
            return RemoveTrees(model, filter, fraction, true);
        }

        public void CutAndDropAllTreesInStand(Model model)
        {
            foreach (MutableTuple<Trees, List<int>> treesOfSpecies in this.mTreesInMostRecentlyLoadedStand)
            {
                Trees trees = treesOfSpecies.Item1;
                foreach (int treeIndex in treesOfSpecies.Item2)
                {
                    trees.SetDeathReasonCutAndDrop(treeIndex); // set flag that tree is cut down
                    trees.Die(model, treeIndex);
                }
            }
            mTreesInMostRecentlyLoadedStand.Clear();
        }

        //private int RemovePercentageFromStand(Model model, int pctFrom, int pctTo, int maxTreesToKill, bool removeBiomassFractions)
        //{
        //    int treesInMostRecentlyLoadedStand = 0;

        //    if (treesInMostRecentlyLoadedStand == 0)
        //    {
        //        return 0;
        //    }
        //    int allTreeIndexFrom = Global.Limit((int)(0.01 * pctFrom * treesInMostRecentlyLoadedStand), 0, treesInMostRecentlyLoadedStand);
        //    int allTreeIndexTo = Global.Limit((int)(0.01 * pctTo * treesInMostRecentlyLoadedStand), 0, treesInMostRecentlyLoadedStand);
        //    if (allTreeIndexFrom >= allTreeIndexTo)
        //    {
        //        // TODO: why not allow removal of a single tree if the indices are the same?
        //        return 0;
        //    }

        //    Debug.WriteLine("attempting to remove " + maxTreesToKill + " trees between indices " + allTreeIndexFrom + " and " + allTreeIndexTo);
        //    int treesKilled = 0;
        //    if (allTreeIndexTo - allTreeIndexFrom <= maxTreesToKill)
        //    {
        //        int allTreeIndex = 0;
        //        foreach (MutableTuple<Trees, List<int>> treesOfSpecies in this.mTreesInMostRecentlyLoadedStand)
        //        {
        //            Trees trees = treesOfSpecies.Item1;
        //            foreach (int treeIndex in treesOfSpecies.Item2)
        //            {
        //                if (allTreeIndex >= allTreeIndexFrom)
        //                {
        //                    if (removeBiomassFractions)
        //                    {
        //                        trees.Remove(model, treeIndex, this.mRemoveFoliage, this.mRemoveBranch, this.mRemoveStem);
        //                    }
        //                    else
        //                    {
        //                        mTreesInMostRecentlyLoadedStand[allTreeIndex].Item1.Remove(model, treeIndex);
        //                    }
        //                }
        //                else if (allTreeIndex > allTreeIndexTo)
        //                {
        //                    break;
        //                }
        //                ++allTreeIndex;
        //            }
        //            if (allTreeIndex > allTreeIndexTo)
        //            {
        //                break;
        //            }
        //        }
        //        treesKilled = allTreeIndexTo - allTreeIndexFrom;
        //    }
        //    else
        //    {
        //        // kill randomly the provided number
        //        while (maxTreesToKill >= 0)
        //        {
        //            int allTreeIndexToKill = model.RandomGenerator.Random(allTreeIndexFrom, allTreeIndexTo);
        //            int allTreeSearchIndex = 0;
        //            foreach (MutableTuple<Trees, List<int>> treesOfSpecies in this.mTreesInMostRecentlyLoadedStand)
        //            {
        //                Trees trees = treesOfSpecies.Item1;
        //                if (allTreeSearchIndex + treesOfSpecies.Item2.Count < allTreeIndexToKill)
        //                {
        //                    int treeIndex = allTreeIndexToKill - allTreeSearchIndex;
        //                    if (trees.IsDead(treeIndex))
        //                    {
        //                        continue;
        //                    }

        //                    if (removeBiomassFractions)
        //                    {
        //                        trees.Remove(model, treeIndex, mRemoveFoliage, mRemoveBranch, mRemoveStem);
        //                    }
        //                    else
        //                    {
        //                        trees.Remove(model, treeIndex);
        //                    }
        //                    ++treesKilled;
        //                }
        //            }

        //            --maxTreesToKill;
        //        }
        //    }

        //    Debug.WriteLine(treesKilled + " trees removed.");
        //    // clean up the tree list...
        //    foreach (MutableTuple<Trees, List<int>> treesOfSpecies in this.mTreesInMostRecentlyLoadedStand)
        //    {
        //        Trees trees = treesOfSpecies.Item1;
        //        foreach (int treeIndex in treesOfSpecies.Item2)
        //        {
        //            if (trees.IsDead(treeIndex))
        //            {
        //                mTreesInMostRecentlyLoadedStand.RemoveAt(treeIndex);
        //            }
        //        }
        //    }

        //    return treesKilled;
        //}

        /** remove trees from a list and reduce the list.
          */
        private int RemoveTrees(Model model, string treeSelectionExpressionString, double removalProbabilityIfSelected, bool management)
        {
            TreeWrapper treeWrapper = new TreeWrapper();
            Expression selectionExpression = new Expression(treeSelectionExpressionString, treeWrapper);
            selectionExpression.EnableIncrementalSum();
            int treesRemoved = 0;
            for (int speciesIndex = 0; speciesIndex < mTreesInMostRecentlyLoadedStand.Count; ++speciesIndex)
            {
                Trees treesOfSpecies = mTreesInMostRecentlyLoadedStand[speciesIndex].Item1;
                treeWrapper.Trees = treesOfSpecies;
                // if expression evaluates to true and if random number below threshold...
                List<int> treeIndices = mTreesInMostRecentlyLoadedStand[speciesIndex].Item2;
                for (int removalIndex = 0; removalIndex < treeIndices.Count; ++removalIndex)
                {
                    int treeIndex = treeIndices[removalIndex];
                    if (selectionExpression.Evaluate(model, treeWrapper) != 0.0 && model.RandomGenerator.Random() <= removalProbabilityIfSelected)
                    {
                        if (management)
                        {
                            treesOfSpecies.Remove(model, treeIndex, mRemoveFoliage, mRemoveBranch, mRemoveStem);
                        }
                        else
                        {
                            treesOfSpecies.Remove(model, treeIndex);
                        }

                        // remove from tree list
                        treeIndices.RemoveAt(removalIndex);
                        --removalIndex;
                        ++treesRemoved;
                    }
                }
            }

            // TODO: why doesn't this compact dead trees as other removal methods do?
            return treesRemoved;
        }

        // calculate aggregates for all trees in the internal list
        //private double AggregateFunction(GlobalSettings globalSettings, string expression, string filter, string type)
        //{
        //    TreeWrapper tw = new TreeWrapper();
        //    Expression expr = new Expression(expression, tw);

        //    double sum = 0.0;
        //    int n = 0;
        //    if (String.IsNullOrEmpty(filter))
        //    {
        //        // without filtering
        //        for (int tp = 0; tp < mTrees.Count; ++tp)
        //        {
        //            tw.Tree = mTrees[tp].Item1;
        //            sum += expr.Calculate(globalSettings);
        //            ++n;
        //            ++tp;
        //        }
        //    }
        //    else
        //    {
        //        // with filtering
        //        Expression filter_expr = new Expression(filter, tw);
        //        filter_expr.EnableIncrementalSum();
        //        for (int tp = 0; tp < mTrees.Count; ++tp)
        //        {
        //            tw.Tree = mTrees[tp].Item1;
        //            if (filter_expr.Calculate(globalSettings) != 0.0)
        //            {
        //                sum += expr.Calculate(globalSettings);
        //                ++n;
        //            }
        //            ++tp;
        //        }
        //    }
        //    if (type == "sum")
        //    {
        //        return sum;
        //    }
        //    if (type == "mean")
        //    {
        //        return n > 0 ? sum / (double)n : 0.0;
        //    }
        //    return 0.0;
        //}

        public void Run()
        {
            this.mTreesInMostRecentlyLoadedStand.Clear();
        }

        public int FilterByTreeID(List<int> treeIDlist)
        {
            List<MutableTuple<Trees, List<int>>> filteredTrees = new List<MutableTuple<Trees, List<int>>>();
            int treesSelected = 0;
            foreach (MutableTuple<Trees, List<int>> treesOfSpecies in this.mTreesInMostRecentlyLoadedStand)
            {
                Trees trees = treesOfSpecies.Item1;
                List<int> treeIndicesInSpecies = null;
                foreach (int treeID in treeIDlist)
                {
                    int treeIndex = trees.ID.IndexOf(treeID);
                    if (treeIndex > -1)
                    {
                        if (treeIndicesInSpecies == null)
                        {
                            treeIndicesInSpecies = new List<int>();
                            filteredTrees.Add(new MutableTuple<Trees, List<int>>(trees, treeIndicesInSpecies));
                        }

                        treeIndicesInSpecies.Add(treeIndex);
                        ++treesSelected;
                    }
                }
            }

            this.mTreesInMostRecentlyLoadedStand = filteredTrees;
            return treesSelected;
        }

        public int Filter(Model model, string filter)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression expr = new Expression(filter, tw);
            expr.EnableIncrementalSum();
            int n_before = mTreesInMostRecentlyLoadedStand.Count;
            for (int tp = 0; tp < mTreesInMostRecentlyLoadedStand.Count; ++tp)
            {
                tw.Trees = mTreesInMostRecentlyLoadedStand[tp].Item1;
                double value = expr.Evaluate(model, tw);
                // keep if expression returns true (1)
                bool keep = value == 1.0;
                // if value is >0 (i.e. not "false"), then draw a random number
                if (!keep && value > 0.0)
                {
                    keep = model.RandomGenerator.Random() < value;
                }
                if (!keep)
                {
                    mTreesInMostRecentlyLoadedStand.RemoveAt(tp);
                    --tp;
                }
                else
                {
                    ++tp;
                }
            }

            Debug.WriteLine("filtering with " + filter + " N=" + n_before + "/" + mTreesInMostRecentlyLoadedStand.Count + " trees (before/after filtering).");
            return mTreesInMostRecentlyLoadedStand.Count;
        }

        //public int LoadResourceUnit(int ruindex)
        //{
        //    Model m = GlobalSettings.Instance.Model;
        //    ResourceUnit ru = m.GetResourceUnit(ruindex);
        //    if (ru == null)
        //    {
        //        return -1;
        //    }
        //    mTrees.Clear();
        //    for (int i = 0; i < ru.Trees.Count; i++)
        //    {
        //        if (!ru.Tree(i).IsDead())
        //        {
        //            mTrees.Add(new MutableTuple<Tree, double>(ru.Tree(i), 0.0));
        //        }
        //    }
        //    return mTrees.Count;
        //}

        //private int Load(string filter, Model model)
        //{
        //    TreeWrapper tw = new TreeWrapper();
        //    mTrees.Clear();
        //    AllTreeIterator at = new AllTreeIterator(model);
        //    if (String.IsNullOrEmpty(filter))
        //    {
        //        for (Tree t = at.MoveNextLiving(); t != null; t = at.MoveNextLiving())
        //        {
        //            if (!t.IsDead())
        //            {
        //                mTrees.Add(new MutableTuple<Tree, double>(t, 0.0));
        //            }
        //        }
        //    }
        //    else
        //    {
        //        Expression expr = new Expression(filter, tw);
        //        expr.EnableIncrementalSum();
        //        Debug.WriteLine("filtering with " + filter);
        //        for (Tree t = at.MoveNextLiving(); t != null; t = at.MoveNextLiving())
        //        {
        //            tw.Tree = t;
        //            if (!t.IsDead() && expr.Execute(model.GlobalSettings) == 0.0)
        //            {
        //                mTrees.Add(new MutableTuple<Tree, double>(t, 0.0));
        //            }
        //        }
        //    }
        //    return mTrees.Count;
        //}

        // loadFromMap: script access
        public void LoadFromMap(MapGridWrapper standWrapper, int standID)
        {
            if (standWrapper == null)
            {
                throw new ArgumentNullException(nameof(standWrapper));
            }
            this.LoadFromMap(standWrapper.StandGrid, standID);
        }

        public void KillSaplings(MapGridWrapper wrap, Model model, int key)
        {
            //MapGridWrapper *wrap = qobject_cast<MapGridWrapper*>(map_grid_object.toQObject());
            //if (!wrap) {
            //    context().throwError("loadFromMap called with invalid map object!");
            //    return;
            //}
            //loadFromMap(wrap.map(), key);
            RectangleF box = wrap.StandGrid.BoundingBox(key);
            GridRunner<float> runner = new GridRunner<float>(model.LightGrid, box);
            for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
            {
                if (wrap.StandGrid.GetStandIDFromLightCoordinate(runner.CurrentIndex()) == key)
                {
                    ResourceUnit ru = null;
                    SaplingCell sc = model.Saplings.Cell(runner.CurrentIndex(), model, true, ref ru);
                    if (sc != null)
                    {
                        model.Saplings.ClearSaplings(sc, ru, true);
                    }
                }
            }
        }

        /// specify removal fractions
        /// @param SWDFrac 0: no change, 1: remove all of standing woody debris
        /// @param DWDfrac 0: no change, 1: remove all of downled woody debris
        /// @param litterFrac 0: no change, 1: remove all of soil litter
        /// @param soilFrac 0: no change, 1: remove all of soil organic matter
        public void RemoveSoilCarbon(MapGridWrapper wrap, int key, float SWDfrac, float DWDfrac, float litterFrac, float soilFrac)
        {
            if (!(SWDfrac >= 0.0 && SWDfrac <= 1.0 && DWDfrac >= 0.0 && DWDfrac <= 1.0 && soilFrac >= 0.0 && soilFrac <= 1.0 && litterFrac >= 0.0 && litterFrac <= 1.0))
            {
                throw new ArgumentException("removeSoilCarbon called with invalid parameters!!");
            }
            List<MutableTuple<ResourceUnit, float>> ruAreas = wrap.StandGrid.ResourceUnitAreas(key).ToList();
            double total_area = 0.0;
            for (int ruIndex = 0; ruIndex < ruAreas.Count; ++ruIndex)
            {
                ResourceUnit ru = ruAreas[ruIndex].Item1;
                float area_factor = ruAreas[ruIndex].Item2; // 0..1
                total_area += area_factor;
                // swd
                if (SWDfrac > 0.0)
                {
                    ru.Snags.RemoveCarbon(SWDfrac * area_factor);
                }
                // soil pools
                ru.Soil.RemoveBiomassFractions(DWDfrac * area_factor, litterFrac * area_factor, soilFrac * area_factor);
                // Debug.WriteLine(ru.index() + area_factor;
            }
            Debug.WriteLine("total area " + total_area + " of " + wrap.StandGrid.Area(key));
        }

        /** slash snags (SWD and otherWood-Pools) of polygon \p key on the map \p wrap.
          The factor is scaled to the overlapping area of \p key on the resource unit.
          @param wrap MapGrid to use together with \p key
          @param key ID of the polygon.
          @param slash_fraction 0: no change, 1: 100%
           */
        public void SlashSnags(MapGridWrapper wrap, int key, float slash_fraction)
        {
            if (slash_fraction < 0.0F || slash_fraction > 1.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(slash_fraction));
            }
            List<MutableTuple<ResourceUnit, float>> ru_areas = wrap.StandGrid.ResourceUnitAreas(key).ToList();
            double total_area = 0.0;
            for (int i = 0; i < ru_areas.Count; ++i)
            {
                ResourceUnit ru = ru_areas[i].Item1;
                float area_factor = ru_areas[i].Item2; // 0..1
                total_area += area_factor;
                ru.Snags.Management(slash_fraction * area_factor);
                // Debug.WriteLine(ru.index() + area_factor;
            }
            Debug.WriteLine("total area " + total_area + " of " + wrap.StandGrid.Area(key));
        }

        /** loadFromMap selects trees located on pixels with value 'key' within the grid 'map_grid'.
            */
        public void LoadFromMap(MapGrid mapGrid, int standID)
        {
            if (mapGrid == null)
            {
                Debug.WriteLine("invalid parameter for loadFromMap: Map expected!");
                return;
            }
            if (mapGrid.IsValid())
            {
                this.mTreesInMostRecentlyLoadedStand = mapGrid.GetLivingTreesInStand(standID);
            }
            else
            {
                Debug.WriteLine("loadFromMap: grid is not valid - no trees loaded");
            }
        }

        //private int TreePairValue(MutableTuple<Trees, List<int>> treesOfSpecies1, MutableTuple<Trees, List<int>> treesOfSpecies2)
        //{
        //    if (treesOfSpecies1.Item2 < treesOfSpecies2.Item2)
        //    {
        //        return -1;
        //    }
        //    if (treesOfSpecies1.Item2 > treesOfSpecies2.Item2)
        //    {
        //        return 1;
        //    }
        //    return 0;
        //}

        //public void Sort(Model model, string sortExpressionString)
        //{
        //    // TODO: replace sort expression with lambda expression?
        //    TreeWrapper treeWrapper = new TreeWrapper();
        //    Expression sorter = new Expression(sortExpressionString, treeWrapper);
        //    // fill the "value" part of the tree storage with a value for each tree
        //    for (int i = 0; i < mTreesInMostRecentlyLoadedStand.Count; ++i)
        //    {
        //        treeWrapper.Trees = mTreesInMostRecentlyLoadedStand[i].Item1;
        //        MutableTuple<Trees, List<int>> tree = mTreesInMostRecentlyLoadedStand[i];
        //        double sortingValue = sorter.Execute(model);
        //    }
        //    // now sort the list....
        //    mTreesInMostRecentlyLoadedStand.Sort(TreePairValue);
        //}

        //public double Percentile(int pct)
        //{
        //    if (mTreesInMostRecentlyLoadedStand.Count == 0)
        //    {
        //        return -1.0;
        //    }
        //    int idx = (int)((pct / 100.0) * mTreesInMostRecentlyLoadedStand.Count);
        //    if (idx >= 0 && idx < mTreesInMostRecentlyLoadedStand.Count)
        //    {
        //        return mTreesInMostRecentlyLoadedStand[idx].Item2;
        //    }
        //    else
        //    {
        //        return -1;
        //    }
        //}

        /// random shuffle of all trees in the list
        //public void Randomize(Model model)
        //{
        //    // fill the "value" part of the tree storage with a random value for each tree
        //    for (int i = 0; i < mTreesInMostRecentlyLoadedStand.Count; ++i)
        //    {
        //        MutableTuple<Trees, double> tree = mTreesInMostRecentlyLoadedStand[i];
        //        tree.Item2 = model.RandomGenerator.Random();
        //    }
        //    // now sort the list....
        //    mTreesInMostRecentlyLoadedStand.Sort(TreePairValue);
        //}
    }
}
