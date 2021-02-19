using iLand.Tools;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace iLand.Simulation
{
    /** @class Management Management executes management routines.
        The actual iLand management is based on Javascript functions. This class provides
        the frame for executing the javascript as well as the functions that are called by scripts and
        that really do the work.
        See http://iland-model.org/iLand+scripting, http://iland-model.org/Object+Management for management Javascript API.
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
            this.mTreesInMostRecentlyLoadedStand = new List<MutableTuple<Trees, List<int>>>();
        }

        // return number of trees currently in list
        public int Count() { return this.mTreesInMostRecentlyLoadedStand.Count; }
        /// calculate the mean value for all trees in the internal list for 'expression' (filtered by the filter criterion)
        // public float Mean(string expression, string filter = null) { return AggregateFunction(expression, filter, "mean"); }
        /// calculate the sum for all trees in the internal list for the 'expression' (filtered by the filter criterion)
        // public float Sum(string expression, string filter = null) { return AggregateFunction(expression, filter, "sum"); }

        // load all trees, return number of trees
        //public int LoadAll()
        //{ 
        //    return Load(null);
        //}

        public static int KillTreesAboveRetentionThreshold(Model model, int treesToRetain)
        {
            AllTreesEnumerator allTreeEnumerator = new AllTreesEnumerator(model.Landscape);
            List<MutableTuple<Trees, int>> livingTrees = new List<MutableTuple<Trees, int>>();
            while (allTreeEnumerator.MoveNextLiving())
            {
                livingTrees.Add(new MutableTuple<Trees, int>(allTreeEnumerator.CurrentTrees, allTreeEnumerator.CurrentTreeIndex));
            }
            int treesToKill = livingTrees.Count - treesToRetain;
            // Debug.WriteLine(livingTrees + " standing, targetsize " + treesToRetain + ", hence " + treesToKill + " trees to remove");
            for (int treesKilled = 0; treesKilled < treesToKill; treesKilled++)
            {
                // TODO: change from O(all trees in model) scaling to O(trees to kill) with data structure for more efficient removal?
                int killIndex = model.RandomGenerator.GetRandomInteger(0, livingTrees.Count);
                livingTrees[killIndex].Item1.Remove(model, livingTrees[killIndex].Item2);
                livingTrees.RemoveAt(killIndex);
            }
            return treesToKill;
        }

        public int KillAllInCurrentStand(Model model, bool removeBiomassFractions)
        {
            int initialTreeCount = this.mTreesInMostRecentlyLoadedStand.Count;
            foreach (MutableTuple<Trees, List<int>> treesOfSpecies in this.mTreesInMostRecentlyLoadedStand)
            {
                // TODO: doesn't check IsCutDown() flag?
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
                    // TODO: why 10%?
                    trees.RemoveDisturbance(model, treeIndex, 0.1F, 0.1F, 0.1F, 0.1F, 1.0F);
                    ++treeCount;
                }
            }
            this.mTreesInMostRecentlyLoadedStand.Clear(); // TODO: why?
            return treeCount;
        }

        public int Kill(Model model, string filter, float fraction)
        {
            return this.RemoveTrees(model, filter, fraction, false);
        }

        public int Manage(Model model, string filter, float fraction)
        {
            return this.RemoveTrees(model, filter, fraction, true);
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
            this.mTreesInMostRecentlyLoadedStand.Clear();
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
        private int RemoveTrees(Model model, string treeSelectionExpressionString, float removalProbabilityIfSelected, bool management)
        {
            TreeWrapper treeWrapper = new TreeWrapper(model);
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
                    treeWrapper.TreeIndex = treeIndex;
                    if (selectionExpression.Evaluate(treeWrapper) != 0.0 && model.RandomGenerator.GetRandomFloat() <= removalProbabilityIfSelected)
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
        //private float AggregateFunction(GlobalSettings globalSettings, string expression, string filter, string type)
        //{
        //    TreeWrapper tw = new TreeWrapper();
        //    Expression expr = new Expression(expression, tw);

        //    float sum = 0.0;
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
        //        return n > 0 ? sum / (float)n : 0.0;
        //    }
        //    return 0.0;
        //}

        public void RunYear()
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
                List<int>? treeIndicesInSpecies = null;
                foreach (int treeID in treeIDlist)
                {
                    int treeIndex = trees.Tag.IndexOf(treeID);
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
            TreeWrapper treeWrapper = new TreeWrapper(model);
            Expression filterExpression = new Expression(filter, treeWrapper);
            filterExpression.EnableIncrementalSum();

            for (int treesOfSpeciesIndex = 0; treesOfSpeciesIndex < this.mTreesInMostRecentlyLoadedStand.Count; ++treesOfSpeciesIndex)
            {
                treeWrapper.Trees = this.mTreesInMostRecentlyLoadedStand[treesOfSpeciesIndex].Item1;
                List<int> standTreeIndices = this.mTreesInMostRecentlyLoadedStand[treesOfSpeciesIndex].Item2;
                for (int standTreeIndex = 0; standTreeIndex < standTreeIndices.Count; ++standTreeIndex)
                {
                    treeWrapper.TreeIndex = standTreeIndices[standTreeIndex];
                    double value = filterExpression.Evaluate(treeWrapper);
                    // keep if expression returns true (1)
                    bool keep = value == 1.0;
                    // if value is >0 (i.e. not "false"), then draw a random number
                    if (!keep && value > 0.0)
                    {
                        keep = model.RandomGenerator.GetRandomFloat() < value;
                    }
                    if (keep == false)
                    {
                        standTreeIndices.RemoveAt(treesOfSpeciesIndex);
                        --standTreeIndex;
                    }
                    else
                    {
                        ++standTreeIndex;
                    }
                }

                if (standTreeIndices.Count == 0)
                {
                    this.mTreesInMostRecentlyLoadedStand.RemoveAt(treesOfSpeciesIndex);
                    --treesOfSpeciesIndex;
                }
            }

            // int totalTreesInStand = mTreesInMostRecentlyLoadedStand.Count;
            // Debug.WriteLine("filtering with " + filter + " N=" + totalTreesInStand + "/" + mTreesInMostRecentlyLoadedStand.Count + " trees (before/after filtering).");
            return this.mTreesInMostRecentlyLoadedStand.Count;
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
        //            mTrees.Add(new MutableTuple<Tree, float>(ru.Tree(i), 0.0));
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
        //                mTrees.Add(new MutableTuple<Tree, float>(t, 0.0));
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
        //                mTrees.Add(new MutableTuple<Tree, float>(t, 0.0));
        //            }
        //        }
        //    }
        //    return mTrees.Count;
        //}

        public static void KillSaplings(MapGrid standGrid, Model model, int key)
        {
            //MapGridWrapper *wrap = qobject_cast<MapGridWrapper*>(map_grid_object.toQObject());
            //if (!wrap) {
            //    context().throwError("loadFromMap called with invalid map object!");
            //    return;
            //}
            //loadFromMap(wrap.map(), key);
            RectangleF boundingBox = standGrid.GetBoundingBox(key);
            GridWindowEnumerator<float> runner = new GridWindowEnumerator<float>(model.Landscape.LightGrid, boundingBox);
            while (runner.MoveNext())
            {
                if (standGrid.GetStandIDFromLightCoordinate(runner.GetCellPosition()) == key)
                {
                    SaplingCell? saplingCell = model.Landscape.GetSaplingCell(runner.GetCellPosition(), true, out ResourceUnit ru);
                    if (saplingCell != null)
                    {
                        ru.ClearSaplings(saplingCell, true);
                    }
                }
            }
        }

        /// specify removal fractions
        /// @param SWDFrac 0: no change, 1: remove all of standing woody debris
        /// @param DWDfrac 0: no change, 1: remove all of downled woody debris
        /// @param litterFrac 0: no change, 1: remove all of soil litter
        /// @param soilFrac 0: no change, 1: remove all of soil organic matter
        public static void RemoveCarbon(MapGrid standGrid, int key, float standingWoodyFraction, float downWoodFraction, float litterFraction, float soilFraction)
        {
            if ((standingWoodyFraction < 0.0F) || (standingWoodyFraction > 1.0F) || 
                (downWoodFraction < 0.0F) || (downWoodFraction > 1.0F) || 
                (soilFraction < 0.0F) || (soilFraction > 1.0F) || 
                (litterFraction > 0.0F && litterFraction > 1.0F))
            {
                throw new ArgumentException("removeSoilCarbon called with one or more invalid parameters.");
            }
            IList<MutableTuple<ResourceUnit, float>> ruAreas = standGrid.GetResourceUnitAreaFractions(key);
            //float totalArea = 0.0F;
            for (int areaIndex = 0; areaIndex < ruAreas.Count; ++areaIndex)
            {
                ResourceUnit ru = ruAreas[areaIndex].Item1;
                if (ru.Soil == null)
                {
                    throw new NotSupportedException("Soil is not enabled on resource unit. Down wood, litter, and soil carbon cannot be removed.");
                }

                float areaFactor = ruAreas[areaIndex].Item2; // 0..1
                //totalArea += areaFactor;
                // swd
                if (standingWoodyFraction > 0.0F)
                {
                    if (ru.Snags == null)
                    {
                        throw new NotSupportedException("Snags are not enabled on resource unit. Standing woody carbon cannot be removed.");
                    }
                    ru.Snags.RemoveCarbon(standingWoodyFraction * areaFactor);
                }
                // soil pools
                ru.Soil.RemoveBiomassFractions(downWoodFraction * areaFactor, litterFraction * areaFactor, soilFraction * areaFactor);
                // Debug.WriteLine(ru.index() + area_factor;
            }
            //Debug.WriteLine("total area " + totalArea + " of " + standGrid.GetArea(key));
        }

        /** slash snags (SWD and otherWood-Pools) of polygon \p key on the map \p wrap.
          The factor is scaled to the overlapping area of \p key on the resource unit.
          @param wrap MapGrid to use together with \p key
          @param key ID of the polygon.
          @param slash_fraction 0: no change, 1: 100%
           */
        public static void SlashSnags(MapGrid standGrid, int key, float slashFraction)
        {
            if (slashFraction < 0.0F || slashFraction > 1.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(slashFraction));
            }
            List<MutableTuple<ResourceUnit, float>> ruAreas = standGrid.GetResourceUnitAreaFractions(key).ToList();
            //float totalArea = 0.0F;
            for (int areaIndex = 0; areaIndex < ruAreas.Count; ++areaIndex)
            {
                ResourceUnit ru = ruAreas[areaIndex].Item1;
                if (ru.Snags == null)
                {
                    throw new NotSupportedException("Snags are not enabled on resource unit so snag to slash conversion is not possible.");
                }

                float area_factor = ruAreas[areaIndex].Item2; // 0..1
                //totalArea += area_factor;
                ru.Snags.TransferStandingWoodToSoil(slashFraction * area_factor);
                // Debug.WriteLine(ru.index() + area_factor;
            }
            //Debug.WriteLine("total area " + totalArea + " of " + standGrid.GetArea(key));
        }

        /** loadFromMap selects trees located on pixels with value 'key' within the grid 'map_grid'.
            */
        public void LoadFromMap(MapGrid mapGrid, int standID)
        {
            if (mapGrid == null)
            {
                throw new ArgumentNullException(nameof(mapGrid));
            }
            if (mapGrid.IsValid())
            {
                this.mTreesInMostRecentlyLoadedStand = mapGrid.GetLivingTreesInStand(standID);
            }
            else
            {
                throw new ArgumentException("Grid is not valid. No trees loaded");
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
        //        float sortingValue = sorter.Execute(model);
        //    }
        //    // now sort the list....
        //    mTreesInMostRecentlyLoadedStand.Sort(TreePairValue);
        //}

        //public float Percentile(int pct)
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
        //        MutableTuple<Trees, float> tree = mTreesInMostRecentlyLoadedStand[i];
        //        tree.Item2 = model.RandomGenerator.Random();
        //    }
        //    // now sort the list....
        //    mTreesInMostRecentlyLoadedStand.Sort(TreePairValue);
        //}
    }
}
