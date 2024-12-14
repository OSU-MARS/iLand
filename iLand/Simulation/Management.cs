// C++/core/{ management.h, management.cpp }
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Simulation
{
    /** @class Management Management executes management routines.
        The actual iLand management is based on Javascript functions. This class provides
        the frame for executing the javascript as well as the functions that are called by scripts and
        that really do the work.
        See https://iland-model.org/iLand+scripting, https://iland-model.org/Object+Management for management Javascript API.
        */
    public class Management
    {
        private List<(TreeListSpatial Trees, List<int> LiveTreeIndices)> treesInMostRecentlyLoadedStand; // C++: mTrees

        // property getter & setter for removal fractions
        /// removal fraction foliage: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        private readonly float removalFractionFoliage;
        /// removal fraction branch biomass: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        private readonly float removalFractionBranch;
        /// removal float stem biomass: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        private readonly float removalFractionStem;

        public Management()
        {
            // default values for removal fractions
            // 100% of the stem, 0% of foliage and branches
            this.removalFractionFoliage = 0.0F;
            this.removalFractionBranch = 0.0F;
            this.removalFractionStem = 1.0F;
            this.treesInMostRecentlyLoadedStand = [];
        }

        public void Clear()
        {
            this.treesInMostRecentlyLoadedStand.Clear();
        }

        // return number of trees currently in list
        public int Count() 
        {
            return this.treesInMostRecentlyLoadedStand.Count; 
        }

        /// calculate the mean value for all trees in the internal list for 'expression' (filtered by the filter criterion)
        // public float Mean(string expression, string filter = null) { return AggregateFunction(expression, filter, "mean"); }
        /// calculate the sum for all trees in the internal list for the 'expression' (filtered by the filter criterion)
        // public float Sum(string expression, string filter = null) { return AggregateFunction(expression, filter, "sum"); }

        // load all trees, return number of trees
        //public int LoadAll()
        //{ 
        //    return Load(null);
        //}

        public static int KillRandomTreesAboveRetentionThreshold(Model model, int treesToRetain)
        {
            AllTreesEnumerator allTreeEnumerator = new(model.Landscape);
            List<(TreeListSpatial Trees, int TreeIndex)> livingTrees = [];
            while (allTreeEnumerator.MoveNextLiving())
            {
                livingTrees.Add(new(allTreeEnumerator.CurrentTrees, allTreeEnumerator.CurrentTreeIndex));
            }
            int treesToKill = livingTrees.Count - treesToRetain;
            // Debug.WriteLine(livingTrees + " standing, targetsize " + treesToRetain + ", hence " + treesToKill + " trees to remove");
            RandomGenerator random = model.RandomGenerator.Value!;
            for (int treesKilled = 0; treesKilled < treesToKill; treesKilled++)
            {
                // TODO: change from O(all trees in model) scaling to O(trees to kill) with data structure for more efficient removal?
                int killIndex = random.GetRandomInteger(0, livingTrees.Count);
                livingTrees[killIndex].Trees.Remove(model, livingTrees[killIndex].TreeIndex);
                livingTrees.RemoveAt(killIndex);
            }
            return treesToKill;
        }

        public int KillAllInCurrentStand(Model model, bool removeBiomassFractions)
        {
            int initialTreeCount = this.treesInMostRecentlyLoadedStand.Count;
            foreach ((TreeListSpatial Trees, List<int> LiveTreeIndices) treesOfSpecies in this.treesInMostRecentlyLoadedStand)
            {
                // TODO: doesn't check IsCutDown() flag?
                TreeListSpatial trees = treesOfSpecies.Trees;
                foreach (int treeIndex in treesOfSpecies.LiveTreeIndices)
                {
                    if (removeBiomassFractions)
                    {
                        trees.Remove(model, treeIndex, this.removalFractionFoliage, this.removalFractionBranch, this.removalFractionStem);
                    }
                    else
                    {
                        trees.Remove(model, treeIndex);
                    }
                }
            }
            this.treesInMostRecentlyLoadedStand.Clear();
            return initialTreeCount;
        }

        public int LethalDisturbanceInCurrentStand(Model model, float stemToSoilFraction, float stemToSnagFraction,
                                                                float branchToSoilFraction, float branchToSnagFraction,
                                                                TreeFlags deathReason) // C++: disturbanceKill()
        {
            bool isFire = (deathReason & TreeFlags.DeadFromFire) == TreeFlags.DeadFromFire;
            int treeCount = 0;
            foreach ((TreeListSpatial Trees, List<int> LiveTreeIndices) liveTreesOfSpecies in this.treesInMostRecentlyLoadedStand)
            {
                TreeListSpatial trees = liveTreesOfSpecies.Trees;
                foreach (int treeIndex in liveTreesOfSpecies.LiveTreeIndices)
                {
                    trees.RemoveDisturbance(model, treeIndex, stemToSnagFraction, stemToSoilFraction, branchToSoilFraction, branchToSnagFraction, foliageToSoilFraction: 1.0F);
                    trees.SetFlags(treeIndex, deathReason);
                    if (isFire && (trees.Species.SeedDispersal != null))
                    {
                        if (trees.Species.IsTreeSerotinousRandom(model.RandomGenerator.Value!, trees.AgeInYears[treeIndex]))
                        {
                            trees.Species.SeedDispersal.SeedProductionSerotiny(model.RandomGenerator.Value!, trees, treeIndex);
                        }
                    }
                    ++treeCount;
                }
            }

            this.treesInMostRecentlyLoadedStand.Clear(); // all trees are removed
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
            foreach ((TreeListSpatial Trees, List<int> LiveTreeIndices) liveTreesOfSpecies in this.treesInMostRecentlyLoadedStand)
            {
                TreeListSpatial trees = liveTreesOfSpecies.Trees;
                foreach (int liveTreeIndex in liveTreesOfSpecies.LiveTreeIndices)
                {
                    trees.SetFlags(liveTreeIndex, TreeFlags.DeadCutAndDrop); // set flag that tree is cut down
                    trees.MarkTreeAsDead(model, liveTreeIndex);
                }
            }
            this.treesInMostRecentlyLoadedStand.Clear();
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
            TreeVariableAccessor treeWrapper = new(model.SimulationState);
            Expression selectionExpression = new(treeSelectionExpressionString, treeWrapper);
            selectionExpression.EnableIncrementalSum();

            RandomGenerator random = model.RandomGenerator.Value!;
            int treesRemoved = 0;
            for (int speciesIndex = 0; speciesIndex < treesInMostRecentlyLoadedStand.Count; ++speciesIndex)
            {
                TreeListSpatial treesOfSpecies = treesInMostRecentlyLoadedStand[speciesIndex].Trees;
                treeWrapper.Trees = treesOfSpecies;
                // if expression evaluates to true and if random number below threshold...
                List<int> treeIndices = treesInMostRecentlyLoadedStand[speciesIndex].LiveTreeIndices;
                for (int removalIndex = 0; removalIndex < treeIndices.Count; ++removalIndex)
                {
                    int treeIndex = treeIndices[removalIndex];
                    treeWrapper.TreeIndex = treeIndex;
                    if (selectionExpression.Evaluate(treeWrapper) != 0.0 && random.GetRandomProbability() <= removalProbabilityIfSelected)
                    {
                        if (management)
                        {
                            treesOfSpecies.Remove(model, treeIndex, removalFractionFoliage, removalFractionBranch, removalFractionStem);
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
        //    TreeWrapper tw = new();
        //    Expression expr = new(expression, tw);

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
        //        Expression filter_expr = new(filter, tw);
        //        filter_expr.EnableIncrementalSum();
        //        for (int tp = 0; tp < mTrees.Count; ++tp)
        //        {
        //            tw.Tree = mTrees[tp].Item1;
        //            if (filter_expr.Calculate(globalSettings) != 0.0F)
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
            this.treesInMostRecentlyLoadedStand.Clear();
        }

        public int FilterByTreeID(List<int> treeIDlist)
        {
            List<(TreeListSpatial Trees, List<int>)> filteredTrees = [];
            int treesSelected = 0;
            foreach ((TreeListSpatial Trees, List<int> LiveTreeIndices) liveTreesOfSpecies in this.treesInMostRecentlyLoadedStand)
            {
                TreeListSpatial trees = liveTreesOfSpecies.Trees;
                List<int>? treeIndicesInSpecies = null;
                foreach (int treeID in treeIDlist)
                {
                    // O(N) search required; trees aren't sorted or indexed by tree ID and multiple trees may have the same ID number
                    for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                    {                        
                        if (trees.TreeID[treeIndex] == treeID)
                        {
                            if (treeIndicesInSpecies == null)
                            {
                                treeIndicesInSpecies = [];
                                filteredTrees.Add(new(trees, treeIndicesInSpecies));
                            }

                            treeIndicesInSpecies.Add(treeIndex);
                            ++treesSelected;
                        }
                    }
                }
            }

            this.treesInMostRecentlyLoadedStand = filteredTrees;
            return treesSelected;
        }

        public int Filter(Model model, string filter)
        {
            TreeVariableAccessor treeWrapper = new(model.SimulationState);
            Expression filterExpression = new(filter, treeWrapper);
            filterExpression.EnableIncrementalSum();

            RandomGenerator random = model.RandomGenerator.Value!;
            for (int treesOfSpeciesIndex = 0; treesOfSpeciesIndex < this.treesInMostRecentlyLoadedStand.Count; ++treesOfSpeciesIndex)
            {
                treeWrapper.Trees = this.treesInMostRecentlyLoadedStand[treesOfSpeciesIndex].Trees;
                List<int> standTreeIndices = this.treesInMostRecentlyLoadedStand[treesOfSpeciesIndex].LiveTreeIndices;
                for (int standTreeIndex = 0; standTreeIndex < standTreeIndices.Count; ++standTreeIndex)
                {
                    treeWrapper.TreeIndex = standTreeIndices[standTreeIndex];
                    float value = filterExpression.Evaluate(treeWrapper);
                    // keep if expression returns true (1)
                    bool keep = value == 1.0F;
                    // if value is >0 (i.e. not "false"), then draw a random number
                    if (!keep && (value > 0.0F))
                    {
                        keep = random.GetRandomProbability() < value;
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
                    this.treesInMostRecentlyLoadedStand.RemoveAt(treesOfSpeciesIndex);
                    --treesOfSpeciesIndex;
                }
            }

            // int totalTreesInStand = mTreesInMostRecentlyLoadedStand.Count;
            // Debug.WriteLine("filtering with " + filter + " N=" + totalTreesInStand + "/" + mTreesInMostRecentlyLoadedStand.Count + " trees (before/after filtering).");
            return this.treesInMostRecentlyLoadedStand.Count;
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
        //    TreeWrapper tw = new();
        //    mTrees.Clear();
        //    AllTreeIterator at = new(model);
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
        //        Expression expr = new(filter, tw);
        //        expr.EnableIncrementalSum();
        //        Debug.WriteLine("filtering with " + filter);
        //        for (Tree t = at.MoveNextLiving(); t != null; t = at.MoveNextLiving())
        //        {
        //            tw.Tree = t;
        //            if (!t.IsDead() && expr.Execute(model.GlobalSettings) == 0.0F)
        //            {
        //                mTrees.Add(new MutableTuple<Tree, float>(t, 0.0F));
        //            }
        //        }
        //    }
        //    return mTrees.Count;
        //}

        public static void KillSaplings(SimulationState simulationState, GridRaster10m standGrid, Model model, UInt32 standID, string? filterExpression)
        {
            RectangleF boundingBox = standGrid.GetBoundingBox(standID);
            GridWindowEnumerator<float> lightGridRunner = new(model.Landscape.LightGrid, boundingBox);

            Expression? filter = null;
            SaplingVariableAccessor? saplingVariableAccessor = null;
            if (String.IsNullOrWhiteSpace(filterExpression) == false)
            {
                saplingVariableAccessor = new SaplingVariableAccessor(simulationState);
                filter = new(filterExpression, saplingVariableAccessor);
            }

            while (lightGridRunner.MoveNext())
            {
                Point lightCellIndexXY = lightGridRunner.GetCurrentXYIndex();
                if (standGrid.GetPolygonIDFromLightGridIndex(lightCellIndexXY) == standID)
                {
                    SaplingCell? saplingCell = model.Landscape.GetSaplingCell(lightCellIndexXY, true, out ResourceUnit ru);
                    if (saplingCell != null)
                    {
                        if (filter == null)
                        {
                            saplingCell.Clear();
                        }
                        else
                        {
                            int nsap_removed = 0;
                            for (int saplingIndex = 0; saplingIndex < saplingCell.Saplings.Length; ++saplingIndex)
                            {
                                Sapling sapling = saplingCell.Saplings[saplingIndex];
                                Debug.Assert(saplingVariableAccessor != null);
                                saplingVariableAccessor.SetSapling(sapling, ru);
                                if (filter.Execute() == 0.0F)
                                {
                                    sapling.Clear();
                                    ++nsap_removed;
                                }
                                if (nsap_removed > 0)
                                {
                                    saplingCell.CheckState();
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void KillAllSaplingsOnResourceUnit(Model model, int ruIndex)
        {
            ResourceUnit ru = model.Landscape.ResourceUnits[ruIndex];
            if (ru.SaplingCells == null)
            {
                throw new InvalidOperationException("Resource unit " + ru.ID + " does not have saplings.");
            }

            for (int saplingCellIndex = 0; saplingCellIndex < ru.SaplingCells.Length; ++saplingCellIndex)
            {
                ru.SaplingCells[saplingCellIndex].Clear();
            }
        }

        /// specify removal fractions
        /// @param SWDFrac 0: no change, 1: remove all of standing woody debris
        /// @param DWDfrac 0: no change, 1: remove all of downed woody debris
        /// @param litterFrac 0: no change, 1: remove all of soil litter
        /// @param soilFrac 0: no change, 1: remove all of soil organic matter
        public static void RemoveCarbon(GridRaster10m standGrid, UInt32 resourceUnitID, float standingWoodyFraction, float downWoodFraction, float litterFraction, float soilFraction)
        {
            if ((standingWoodyFraction < 0.0F) || (standingWoodyFraction > 1.0F) || 
                (downWoodFraction < 0.0F) || (downWoodFraction > 1.0F) || 
                (soilFraction < 0.0F) || (soilFraction > 1.0F) || 
                (litterFraction > 0.0F && litterFraction > 1.0F))
            {
                throw new ArgumentException("removeSoilCarbon called with one or more invalid parameters.");
            }
            IList<(ResourceUnit, float)> ruAreas = standGrid.GetResourceUnitAreaFractions(resourceUnitID);
            //float totalArea = 0.0F;
            for (int areaIndex = 0; areaIndex < ruAreas.Count; ++areaIndex)
            {
                (ResourceUnit resourceUnit, float areaFactor) = ruAreas[areaIndex];
                if (resourceUnit.Soil == null)
                {
                    throw new NotSupportedException("Soil is not enabled on resource unit. Down wood, litter, and soil carbon cannot be removed.");
                }

                //totalArea += areaFactor;
                // standing woody debris, if enabled
                if (standingWoodyFraction > 0.0F)
                {
                    resourceUnit.Snags?.RemoveCarbon(standingWoodyFraction * areaFactor);
                }
                // soil pools, if enabled
                resourceUnit.Soil?.RemoveBiomassFractions(downWoodFraction * areaFactor, litterFraction * areaFactor, soilFraction * areaFactor);
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
        public static void SlashSnags(GridRaster10m standGrid, UInt32 resourceUnitID, float slashFraction)
        {
            if (slashFraction < 0.0F || slashFraction > 1.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(slashFraction));
            }
            List<(ResourceUnit, float)> ruAreas = standGrid.GetResourceUnitAreaFractions(resourceUnitID);
            //float totalArea = 0.0F;
            for (int areaIndex = 0; areaIndex < ruAreas.Count; ++areaIndex)
            {
                (ResourceUnit resourceUnit, float areaFactor) = ruAreas[areaIndex];
                if (resourceUnit.Snags == null)
                {
                    throw new NotSupportedException("Snags are not enabled on resource unit so snag to slash conversion is not possible.");
                }

                //totalArea += area_factor;
                resourceUnit.Snags.TransferStandingWoodToSoil(slashFraction * areaFactor);
                // Debug.WriteLine(ru.index() + area_factor;
            }
            // Debug.WriteLine("total area " + totalArea + " of " + standGrid.GetArea(key));
        }

        /** loadFromMap selects trees located on pixels with value 'key' within the grid 'map_grid'.
            */
        public void LoadFromMap(GridRaster10m mapGrid, UInt32 standID, bool append)
        {
            if (mapGrid.IsSetup() == false)
            {
                throw new ArgumentException("Grid is not valid. No trees loaded", nameof(mapGrid));
            }

            List<(TreeListSpatial, List<int>)> trees = mapGrid.GetLivingTreesInStand(standID);
            this.LoadFromTreeList(trees, append);
        }

        private void LoadFromTreeList(List<(TreeListSpatial, List<int>)> tree_list, bool append)
        {
            if (append == false)
            {
                this.treesInMostRecentlyLoadedStand = tree_list;
            }
            else
            {
                this.treesInMostRecentlyLoadedStand.AddRange(tree_list);
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
        //    TreeWrapper treeWrapper = new();
        //    Expression sorter = new(sortExpressionString, treeWrapper);
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
