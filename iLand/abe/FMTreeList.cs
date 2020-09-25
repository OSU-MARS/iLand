using iLand.core;
using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.abe
{
    /** @class FMTreeList
        @ingroup abe
        The FMTreeList class implements low-level functionality for selecting and harvesting of trees.
        The functions of the class are usually accessed via Javascript.
        */
    internal class FMTreeList
    {
        private readonly List<MutableTuple<Tree, double>> mTrees; ///< store a Tree-pointer and a value (e.g. for sorting)
        // unused in C++
        //private bool mResourceUnitsLocked;
        //private int mRemoved;
        private FMStand mStand; /// the stand the list is currently connected
        private int mStandId; ///< link to active stand
        private int mNumberOfStems; ///< estimate for the number of trees in the stand
        private bool mOnlySimulate; ///< mode
        private RectangleF mStandRect;
        private readonly Grid<float> mStandGrid; ///< local stand grid (10m pixel)
        private readonly Grid<int> mTreeCountGrid; ///< tree counts on local stand grid (10m)
        private readonly Grid<float> mLocalGrid; ///< 2m grid of the stand
        private Expression mRunGridCustom;

        public int standId() { return mStandId; }
        public bool simulate() { return mOnlySimulate; }
        public void setSimulate(bool do_simulate) { mOnlySimulate = do_simulate; }
        public int count() { return mTrees.Count; }

        /// access the list of trees
        public List<MutableTuple<Tree, double>> trees() { return mTrees; }

        /// access to local grid (setup if necessary)
        public Grid<float> localGrid() { PrepareGrids(); return mLocalGrid; }

        /// load all trees of the stand, return number of trees (living trees)
        public int loadAll() { return Load(null); }

        public Grid<float> standGrid() { return mStandGrid; }

        // TODO: fix: removal fractions need to be moved to agent/units/ whatever....
        public double removeFoliage() { return 0.0; }
        public double removeStem() { return 1.0; }
        public double removeBranch() { return 0.0; }

        public FMTreeList()
        {
            this.mLocalGrid = new Grid<float>();
            this.mStandGrid = new Grid<float>();
            this.mTreeCountGrid = new Grid<int>();
            this.mTrees = new List<MutableTuple<Tree, double>>();

            SetStand(null); // clear stand link, sets mStand
        }

        public FMTreeList(FMStand stand)
            : this()
        {
            SetStand(stand);
        }

        /// calculate the mean value for all trees in the internal list for 'expression' (filtered by the filter criterion)
        public double GetMean(string expression, string filter = null) { return AggregateFunction(expression, filter, "mean"); }
        /// calculate the sum for all trees in the internal list for the 'expression' (filtered by the filter criterion)
        public double Sum(string expression, string filter = null) { return AggregateFunction(expression, filter, "sum"); }

        public void SetStand(FMStand stand)
        {
            CheckLocks();
            mStand = stand;
            if (stand != null)
            {
                mStandId = stand.id();
                mNumberOfStems = (int)(stand.stems() * stand.area());
                mOnlySimulate = stand.CurrentActivity() != null && stand.currentFlags().IsScheduled();
                mStandRect = new RectangleF(); // BUGBUG: why isn't size set?
            }
            else
            {
                mStandId = -1;
                mNumberOfStems = 1000;
                mOnlySimulate = false;
            }
        }

        public int Load(string filter)
        {
            if (standId() > -1)
            {
                // load all trees of the current stand
                MapGrid map = ForestManagementEngine.StandGrid();
                if (map.IsValid())
                {
                    map.LoadTrees(mStandId, mTrees, filter, mNumberOfStems);
                    // mResourceUnitsLocked = true;
                }
                else
                {
                    Debug.WriteLine("load: grid is not valid - no trees loaded");
                }
                return mTrees.Count;
            }
            else
            {
                Debug.WriteLine("load: loading *all* trees, because stand id is -1");
                TreeWrapper tw = new TreeWrapper();
                Model m = GlobalSettings.Instance.Model;
                mTrees.Clear();
                AllTreeIterator at = new AllTreeIterator(m);
                if (String.IsNullOrEmpty(filter))
                {
                    for (Tree t = at.MoveNextLiving(); t != null; t = at.MoveNextLiving())
                    {
                        if (!t.IsDead())
                        {
                            mTrees.Add(new MutableTuple<Tree, double>(t, 0.0));
                        }
                    }
                }
                else
                {
                    Expression expr = new Expression(filter, tw);
                    expr.EnableIncrementalSum();
                    Debug.WriteLine("filtering with " + filter);
                    for (Tree t = at.MoveNextLiving(); t != null; t = at.MoveNextLiving())
                    {
                        tw.Tree = t;
                        if (!t.IsDead() && expr.Execute() != 0.0)
                        {
                            mTrees.Add(new MutableTuple<Tree, double>(t, 0.0));
                        }
                    }
                }
                return mTrees.Count;
            }
        }

        public int RemoveMarkedTrees()
        {
            loadAll();
            int n_removed = 0;
            for (int it = 0; it < mTrees.Count; ++it)
            {
                Tree t = mTrees[it].Item1;
                if (t.IsMarkedForCut())
                {
                    t.Remove();
                    n_removed++;
                }
                else if (t.IsMarkedForHarvest())
                {
                    t.Remove(removeFoliage(), removeBranch(), removeStem());
                    n_removed++;
                }
            }
            if (mStand.TracingEnabled())
            {
                Debug.WriteLine(mStand.context() + " removeMarkedTrees: n=" + n_removed);
            }
            return n_removed;
        }

        public int Kill(string filter)
        {
            return RemoveTrees(filter, 1.0, false);
        }

        public int Harvest(string filter, double fraction)
        {
            return RemoveTrees(filter, fraction, true);
        }

        // unused in C++
        //private bool trace()
        //{
        //    return FomeScript.bridge().standObj().trace();
        //}

        // unused in C++
        //private int remove_percentiles(int pctfrom, int pctto, int number, bool management)
        //{
        //    if (mTrees.Count == 0)
        //    {
        //        return 0;
        //    }
        //    int index_from = Global.limit((int)(pctfrom / 100.0 * mTrees.Count), 0, mTrees.Count);
        //    int index_to = Global.limit((int)(pctto / 100.0 * mTrees.Count), 0, mTrees.Count - 1);
        //    if (index_from >= index_to)
        //    {
        //        return 0;
        //    }

        //    //Debug.WriteLine("attempting to remove" + number + "trees between indices" + index_from + "and" + index_to;
        //    int count = number;
        //    if (index_to - index_from <= number)
        //    {
        //        // kill all
        //        if (management)
        //        {
        //            // management
        //            for (int i = index_from; i < index_to; i++)
        //            {
        //                if (simulate())
        //                {
        //                    mTrees[i].Item1.markForHarvest(true);
        //                    mStand.addScheduledHarvest(mTrees[i].Item1.volume());
        //                }
        //                else
        //                {
        //                    mTrees[i].Item1.remove(removeFoliage(), removeBranch(), removeStem());
        //                }
        //            }
        //        }
        //        else
        //        {
        //            // just kill...
        //            for (int i = index_from; i < index_to; i++)
        //            {
        //                if (simulate())
        //                {
        //                    mTrees[i].Item1.markForCut(true);
        //                    mStand.addScheduledHarvest(mTrees[i].Item1.volume());
        //                }
        //                else
        //                {
        //                    mTrees[i].Item1.remove();
        //                }
        //            }
        //        }
        //        count = index_to - index_from;
        //    }
        //    else
        //    {
        //        // kill randomly the provided number
        //        int cancel = 1000;
        //        while (number >= 0)
        //        {
        //            int rnd_index = RandomGenerator.irandom(index_from, index_to);
        //            Tree tree = mTrees[rnd_index].Item1;
        //            if (tree.isDead() || tree.isMarkedForHarvest() || tree.isMarkedForCut())
        //            {
        //                if (--cancel < 0)
        //                {
        //                    Debug.WriteLine("Management::kill: canceling search. " + number + " trees left.");
        //                    count -= number; // not all trees were killed
        //                    break;
        //                }
        //                continue;
        //            }
        //            cancel = 1000;
        //            number--;
        //            if (management)
        //            {
        //                if (simulate())
        //                {
        //                    tree.markForHarvest(true);
        //                    mStand.addScheduledHarvest(tree.volume());
        //                }
        //                else
        //                {
        //                    tree.remove(removeFoliage(), removeBranch(), removeStem());
        //                }
        //            }
        //            else
        //            {
        //                if (simulate())
        //                {
        //                    tree.markForCut(true);
        //                    mStand.addScheduledHarvest(tree.volume());
        //                }
        //                else
        //                {
        //                    tree.remove();
        //                }
        //            }
        //        }
        //    }
        //    if (mStand != null && mStand.trace())
        //    {
        //        Debug.WriteLine("remove_percentiles: " + count + " removed.");
        //    }

        //    // clean up the tree list...
        //    for (int i = mTrees.Count - 1; i >= 0; --i)
        //    {
        //        if (mTrees[i].Item1.isDead())
        //        {
        //            mTrees.RemoveAt(i);
        //        }
        //    }

        //    return count; // killed or cut
        //}

        /** remove trees from a list and reduce the list.
          */
        private int RemoveTrees(string expression, double fraction, bool management)
        {
            if (String.IsNullOrEmpty(expression))
            {
                expression = "true";
            }

            TreeWrapper tw = new TreeWrapper();
            Expression expr = new Expression(expression, tw);
            expr.EnableIncrementalSum();
            int n = 0;
            for (int index = 0; index < mTrees.Count; ++index)
            {
                MutableTuple<Tree, double> tp = mTrees[index];
                tw.Tree = tp.Item1;
                // if expression evaluates to true and if random number below threshold...
                if (expr.Calculate(tw) != 0.0 && RandomGenerator.Random() <= fraction)
                {
                    // remove from system
                    if (management)
                    {
                        if (simulate())
                        {
                            tp.Item1.MarkForHarvest(true);
                            mStand.AddScheduledHarvest(tp.Item1.Volume());
                        }
                        else
                        {
                            tp.Item1.MarkForHarvest(true);
                            tp.Item1.Remove(removeFoliage(), removeBranch(), removeStem()); // management with removal fractions
                        }
                    }
                    else
                    {
                        if (simulate())
                        {
                            tp.Item1.MarkForCut(true);
                            tp.Item1.SetDeathReasonCutdown();
                            mStand.AddScheduledHarvest(tp.Item1.Volume());
                        }
                        else
                        {
                            tp.Item1.MarkForCut(true);
                            tp.Item1.SetDeathReasonCutdown();
                            tp.Item1.Remove(); // kill
                        }
                    }

                    // remove from tree list
                    mTrees.RemoveAt(index--);
                    n++;
                }
            }

            return n;
        }

        private double AggregateFunction(string expression, string filter, string type)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression expr = new Expression(expression, tw);

            double sum = 0.0;
            int n = 0;
            if (String.IsNullOrEmpty(filter))
            {
                // without filtering
                foreach (MutableTuple<Tree, double> tp in mTrees)
                {
                    tw.Tree = tp.Item1;
                    sum += expr.Calculate();
                    ++n;
                }
            }
            else
            {
                // with filtering
                Expression filter_expr = new Expression(filter, tw);
                filter_expr.EnableIncrementalSum();
                foreach (MutableTuple<Tree, double> tp in mTrees)
                {
                    tw.Tree = tp.Item1;
                    if (filter_expr.Calculate() != 0.0)
                    {
                        sum += expr.Calculate();
                        ++n;
                    }
                }
            }

            if (type == "sum")
            {
                return sum;
            }
            if (type == "mean")
            {
                return n > 0 ? sum / (double)n : 0.0;
            }
            return 0.0;
        }

        public bool RemoveSingleTree(int index, bool harvest)
        {
            if (mStand == null || index < 0 || index >= mTrees.Count)
            {
                return false;
            }

            Tree tree = mTrees[index].Item1;
            if (harvest)
            {
                if (simulate())
                {
                    tree.MarkForHarvest(true);
                    mStand.AddScheduledHarvest(tree.Volume());
                }
                else
                {
                    tree.Remove(removeFoliage(), removeBranch(), removeStem());
                }
            }
            else
            {
                if (simulate())
                {
                    tree.MarkForCut(true);
                    mStand.AddScheduledHarvest(tree.Volume());
                }
                else
                {
                    tree.Remove();
                }
            }
            return true;
        }

        private int CompareTreePairValue(MutableTuple<Tree, double> p1, MutableTuple<Tree, double> p2)
        {
            if (p1.Item2 < p2.Item2)
            {
                return -1;
            }
            if (p1.Item2 > p2.Item2)
            {
                return 1;
            }
            return 0;
        }

        public void Sort(string statement)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression sorter = new Expression(statement, tw);
            // fill the "value" part of the tree storage with a value for each tree
            for (int i = 0; i < mTrees.Count; ++i)
            {
                MutableTuple<Tree, double> it =  mTrees[i];
                tw.Tree = it.Item1;
                it.Item2 = sorter.Execute();
            }
            // now sort the list....
            mTrees.Sort(CompareTreePairValue);
        }

        public double Percentile(int pct)
        {
            if (mTrees.Count == 0)
            {
                return -1.0;
            }
            int idx = (int)((pct / 100.0) * mTrees.Count);
            if (idx >= 0 && idx < mTrees.Count)
            {
                return mTrees[idx].Item2;
            }
            else
            {
                return -1;
            }
        }

        /// random shuffle of all trees in the list
        public void Randomize()
        {
            // fill the "value" part of the tree storage with a random value for each tree
            for (int i = 0; i < mTrees.Count; ++i)
            {
                MutableTuple<Tree, double> it = mTrees[i];
                it.Item2 = RandomGenerator.Random();
            }

            // now sort the list....
            mTrees.Sort(CompareTreePairValue);
        }

        private void PrepareGrids()
        {
            RectangleF box = ForestManagementEngine.StandGrid().BoundingBox(mStand.id());
            if (mStandRect == box)
            {
                return;
            }
            mStandRect = box;
            // the memory of the grids is only reallocated if the current box is larger then the previous...
            mStandGrid.Setup(box, Constant.HeightSize);
            mTreeCountGrid.Setup(box, Constant.HeightSize);
            mLocalGrid.Setup(box, Constant.LightSize);
            // mark areas outside of the grid...
            GridRunner<int> runner = new GridRunner<int>(ForestManagementEngine.StandGrid().Grid, box);
            for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
            {
                int p = runner.Current;
                if (runner.Current != mStand.id())
                {
                    p = -1;
                }
                runner.Current = ++p;
            }
            // copy stand limits to the grid
            for (int iy = 0; iy < mLocalGrid.SizeY; ++iy)
            {
                for (int ix = 0; ix < mLocalGrid.SizeX; ++ix)
                {
                    mLocalGrid[ix, iy] = mStandGrid[ix / Constant.LightPerHeightSize, iy / Constant.LightPerHeightSize] == -1.0F ? -1.0F : 0.0F;
                }
            }
        }

        private delegate void GridAction(ref float cell, ref int n, Tree tree, FMTreeList list);
        private void RunGrid(GridAction func)
        {
            if (mStandRect == null)
            {
                PrepareGrids();
            }

            // set all values to 0 (within the limits of the stand grid)
            for (int p = 0; p < mStandGrid.Count; ++p)
            {
                if (mStandGrid[p] != -1.0F)
                {
                    mStandGrid[p] = 0.0F;
                }
            }
            mTreeCountGrid.Initialize(0);
            int invalid_index = 0;
            foreach (MutableTuple<Tree, double> it in mTrees)
            {
                Tree tree = it.Item1;
                Point p = mStandGrid.IndexAt(tree.GetCellCenterPoint());
                if (mStandGrid.Contains(p))
                {
                    float cell = mStandGrid[p];
                    int n = mTreeCountGrid[p];
                    func.Invoke(ref cell, ref n, tree, this);
                    mStandGrid[p] = cell;
                    mTreeCountGrid[p] = n;
                }
                else
                {
                    ++invalid_index;
                }
            }
            if (invalid_index != 0)
            {
                Debug.WriteLine("runGrid: invalid index: n=" + invalid_index);
            }

            // finalization: call again for each *cell*
            for (int i = 0; i < mStandGrid.Count; ++i)
            {
                float cell = mStandGrid[i];
                int n = mTreeCountGrid[i];
                func.Invoke(ref cell, ref n, null, this);
                mStandGrid[i] = cell;
                mTreeCountGrid[i] = n;
            }
        }

        public void RunGridHeightMax(ref float cell, ref int n, Tree tree, FMTreeList list)
        {
            if (tree != null)
            {
                cell = Math.Max(cell, tree.Height);
            }
        }

        public void RunGridBasalArea(ref float cell, ref int n, Tree tree, FMTreeList list)
        {
            if (tree != null)
            {
                cell += (float)tree.BasalArea();
                ++n;
            }
            else
            {
                if (n > 0)
                {
                    cell /= (float)n;
                }
            }
        }

        public void RunGridVolume(ref float cell, ref int n, Tree tree, FMTreeList list)
        {
            if (tree != null)
            {
                cell += (float)tree.Volume();
                ++n;
            }
            else
            {
                if (n > 0)
                {
                    cell /= (float)n;
                }
            }
        }

        public void RunGridCustom(ref float cell, ref int n, Tree tree, FMTreeList list)
        {
            if (tree != null)
            {
                TreeWrapper tw = new TreeWrapper(tree);
                cell = (float)list.mRunGridCustom.Calculate(tw);
                ++n;
            }
        }

        public void PrepareStandGrid(string type, string custom_expression)
        {
            if (mStand != null)
            {
                Debug.WriteLine("Error: FMTreeList: no current stand defined.");
                return;
            }

            if (type == "height")
            {
                RunGrid(RunGridHeightMax);
            }
            else if (type == "basalArea")
            {
                RunGrid(RunGridBasalArea);
            }
            else if (type == "volume")
            {
                RunGrid(RunGridVolume);
            }
            else if (type == "custom")
            {
                mRunGridCustom = new Expression(custom_expression);
                RunGrid(RunGridCustom);
                mRunGridCustom = null;
            }
            else
            {
                Debug.WriteLine("FMTreeList: invalid type for prepareStandGrid: " + type);
            }
        }

        public void ExportStandGrid(string file_name)
        {
            file_name = GlobalSettings.Instance.Path(file_name);
            Helper.SaveToTextFile(file_name, Grid.ToEsriRaster(mStandGrid));
            Debug.WriteLine("saved grid to file " + file_name);
        }

        private void CheckLocks()
        {
            // removed the locking code again, WR20140821
            //    if (mStand && mResourceUnitsLocked) {
            //        MapGrid *map = ForestManagementEngine.instance().standGrid();
            //        if (map.isValid()) {
            //            map.freeLocksForStand(mStandId);
            //            mResourceUnitsLocked = false;
            //        }
            //    }
        }
    }
}
