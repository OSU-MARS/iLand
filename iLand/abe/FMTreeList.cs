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
        private List<MutableTuple<Tree, double>> mTrees; ///< store a Tree-pointer and a value (e.g. for sorting)
        private bool mResourceUnitsLocked;
        private int mRemoved;
        private FMStand mStand; /// the stand the list is currently connected
        private int mStandId; ///< link to active stand
        private int mNumberOfStems; ///< estimate for the number of trees in the stand
        private bool mOnlySimulate; ///< mode
        private RectangleF mStandRect;
        private Grid<float> mStandGrid; ///< local stand grid (10m pixel)
        private Grid<int> mTreeCountGrid; ///< tree counts on local stand grid (10m)
        private Grid<float> mLocalGrid; ///< 2m grid of the stand
        private Expression mRunGridCustom;
        private double mRunGridCustomCell;

        public int standId() { return mStandId; }
        public bool simulate() { return mOnlySimulate; }
        public void setSimulate(bool do_simulate) { mOnlySimulate = do_simulate; }
        public int count() { return mTrees.Count; }

        /// access the list of trees
        public List<MutableTuple<Tree, double>> trees() { return mTrees; }

        /// access to local grid (setup if necessary)
        public Grid<float> localGrid() { prepareGrids(); return mLocalGrid; }

        /// load all trees of the stand, return number of trees (living trees)
        public int loadAll() { return load(null); }

        /// calculate the mean value for all trees in the internal list for 'expression' (filtered by the filter criterion)
        public double mean(string expression, string filter = null) { return aggregate_function(expression, filter, "mean"); }
        /// calculate the sum for all trees in the internal list for the 'expression' (filtered by the filter criterion)
        public double sum(string expression, string filter = null) { return aggregate_function(expression, filter, "sum"); }

        public Grid<float> standGrid() { return mStandGrid; }

        // TODO: fix: removal fractions need to be moved to agent/units/ whatever....
        public double removeFoliage() { return 0.0; }
        public double removeStem() { return 1.0; }
        public double removeBranch() { return 0.0; }

        public FMTreeList(object parent = null)
        {
            mStand = null;
            setStand(null); // clear stand link
            mResourceUnitsLocked = false;
        }

        public FMTreeList(FMStand stand, object parent)
            : this(parent)
        {
            setStand(stand);
        }

        public void setStand(FMStand stand)
        {
            check_locks();
            mStand = stand;
            if (stand != null)
            {
                mStandId = stand.id();
                mNumberOfStems = (int)(stand.stems() * stand.area());
                mOnlySimulate = stand.currentActivity() != null ? stand.currentFlags().isScheduled() : false;
                mStandRect = new RectangleF(); // BUGBUG: why isn't size set?
            }
            else
            {
                mStandId = -1;
                mNumberOfStems = 1000;
                mOnlySimulate = false;
            }
        }

        public int load(string filter)
        {
            if (standId() > -1)
            {
                // load all trees of the current stand
                MapGrid map = ForestManagementEngine.standGrid();
                if (map.isValid())
                {
                    map.loadTrees(mStandId, mTrees, filter, mNumberOfStems);
                    mResourceUnitsLocked = true;
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
                Model m = GlobalSettings.instance().model();
                mTrees.Clear();
                AllTreeIterator at = new AllTreeIterator(m);
                if (String.IsNullOrEmpty(filter))
                {
                    for (Tree t = at.nextLiving(); t != null; t = at.nextLiving())
                    {
                        if (!t.isDead())
                        {
                            mTrees.Add(new MutableTuple<Tree, double>(t, 0.0));
                        }
                    }
                }
                else
                {
                    Expression expr = new Expression(filter, tw);
                    expr.enableIncSum();
                    Debug.WriteLine("filtering with " + filter);
                    for (Tree t = at.nextLiving(); t != null; t = at.nextLiving())
                    {
                        tw.setTree(t);
                        if (!t.isDead() && expr.execute() != 0.0)
                        {
                            mTrees.Add(new MutableTuple<Tree, double>(t, 0.0));
                        }
                    }
                }
                return mTrees.Count;
            }
        }

        public int removeMarkedTrees()
        {
            loadAll();
            int n_removed = 0;
            for (int it = 0; it < mTrees.Count; ++it)
            {
                Tree t = mTrees[it].Item1;
                if (t.isMarkedForCut())
                {
                    t.remove();
                    n_removed++;
                }
                else if (t.isMarkedForHarvest())
                {
                    t.remove(removeFoliage(), removeBranch(), removeStem());
                    n_removed++;
                }
            }
            if (mStand.trace())
            {
                Debug.WriteLine(mStand.context() + " removeMarkedTrees: n=" + n_removed);
            }
            return n_removed;
        }

        public int kill(string filter)
        {
            return remove_trees(filter, 1.0, false);
        }

        public int harvest(string filter, double fraction)
        {
            return remove_trees(filter, fraction, true);
        }

        private bool trace()
        {
            return FomeScript.bridge().standObj().trace();
        }

        private int remove_percentiles(int pctfrom, int pctto, int number, bool management)
        {
            if (mTrees.Count == 0)
            {
                return 0;
            }
            int index_from = Global.limit((int)(pctfrom / 100.0 * mTrees.Count), 0, mTrees.Count);
            int index_to = Global.limit((int)(pctto / 100.0 * mTrees.Count), 0, mTrees.Count - 1);
            if (index_from >= index_to)
            {
                return 0;
            }

            //Debug.WriteLine("attempting to remove" + number + "trees between indices" + index_from + "and" + index_to;
            int count = number;
            if (index_to - index_from <= number)
            {
                // kill all
                if (management)
                {
                    // management
                    for (int i = index_from; i < index_to; i++)
                    {
                        if (simulate())
                        {
                            mTrees[i].Item1.markForHarvest(true);
                            mStand.addScheduledHarvest(mTrees[i].Item1.volume());
                        }
                        else
                        {
                            mTrees[i].Item1.remove(removeFoliage(), removeBranch(), removeStem());
                        }
                    }
                }
                else
                {
                    // just kill...
                    for (int i = index_from; i < index_to; i++)
                    {
                        if (simulate())
                        {
                            mTrees[i].Item1.markForCut(true);
                            mStand.addScheduledHarvest(mTrees[i].Item1.volume());
                        }
                        else
                        {
                            mTrees[i].Item1.remove();
                        }
                    }
                }
                count = index_to - index_from;
            }
            else
            {
                // kill randomly the provided number
                int cancel = 1000;
                while (number >= 0)
                {
                    int rnd_index = RandomGenerator.irandom(index_from, index_to);
                    Tree tree = mTrees[rnd_index].Item1;
                    if (tree.isDead() || tree.isMarkedForHarvest() || tree.isMarkedForCut())
                    {
                        if (--cancel < 0)
                        {
                            Debug.WriteLine("Management::kill: canceling search. " + number + " trees left.");
                            count -= number; // not all trees were killed
                            break;
                        }
                        continue;
                    }
                    cancel = 1000;
                    number--;
                    if (management)
                    {
                        if (simulate())
                        {
                            tree.markForHarvest(true);
                            mStand.addScheduledHarvest(tree.volume());
                        }
                        else
                        {
                            tree.remove(removeFoliage(), removeBranch(), removeStem());
                        }
                    }
                    else
                    {
                        if (simulate())
                        {
                            tree.markForCut(true);
                            mStand.addScheduledHarvest(tree.volume());
                        }
                        else
                        {
                            tree.remove();
                        }
                    }
                }
            }
            if (mStand != null && mStand.trace())
            {
                Debug.WriteLine("remove_percentiles: " + count + " removed.");
            }

            // clean up the tree list...
            for (int i = mTrees.Count - 1; i >= 0; --i)
            {
                if (mTrees[i].Item1.isDead())
                {
                    mTrees.RemoveAt(i);
                }
            }

            return count; // killed or cut
        }

        /** remove trees from a list and reduce the list.
          */
        private int remove_trees(string expression, double fraction, bool management)
        {
            if (String.IsNullOrEmpty(expression))
            {
                expression = "true";
            }

            TreeWrapper tw = new TreeWrapper();
            Expression expr = new Expression(expression, tw);
            expr.enableIncSum();
            int n = 0;
            for (int index = 0; index < mTrees.Count; ++index)
            {
                MutableTuple<Tree, double> tp = mTrees[index];
                tw.setTree(tp.Item1);
                // if expression evaluates to true and if random number below threshold...
                if (expr.calculate(tw) != 0.0 && RandomGenerator.drandom() <= fraction)
                {
                    // remove from system
                    if (management)
                    {
                        if (simulate())
                        {
                            tp.Item1.markForHarvest(true);
                            mStand.addScheduledHarvest(tp.Item1.volume());
                        }
                        else
                        {
                            tp.Item1.markForHarvest(true);
                            tp.Item1.remove(removeFoliage(), removeBranch(), removeStem()); // management with removal fractions
                        }
                    }
                    else
                    {
                        if (simulate())
                        {
                            tp.Item1.markForCut(true);
                            tp.Item1.setDeathCutdown();
                            mStand.addScheduledHarvest(tp.Item1.volume());
                        }
                        else
                        {
                            tp.Item1.markForCut(true);
                            tp.Item1.setDeathCutdown();
                            tp.Item1.remove(); // kill
                        }
                    }

                    // remove from tree list
                    mTrees.RemoveAt(index--);
                    n++;
                }
            }

            return n;
        }

        private double aggregate_function(string expression, string filter, string type)
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
                    tw.setTree(tp.Item1);
                    sum += expr.calculate();
                    ++n;
                }
            }
            else
            {
                // with filtering
                Expression filter_expr = new Expression(filter, tw);
                filter_expr.enableIncSum();
                foreach (MutableTuple<Tree, double> tp in mTrees)
                {
                    tw.setTree(tp.Item1);
                    if (filter_expr.calculate() != 0.0)
                    {
                        sum += expr.calculate();
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

        public bool remove_single_tree(int index, bool harvest)
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
                    tree.markForHarvest(true);
                    mStand.addScheduledHarvest(tree.volume());
                }
                else
                {
                    tree.remove(removeFoliage(), removeBranch(), removeStem());
                }
            }
            else
            {
                if (simulate())
                {
                    tree.markForCut(true);
                    mStand.addScheduledHarvest(tree.volume());
                }
                else
                {
                    tree.remove();
                }
            }
            return true;
        }

        private int treePairValue(MutableTuple<Tree, double> p1, MutableTuple<Tree, double> p2)
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

        public void sort(string statement)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression sorter = new Expression(statement, tw);
            // fill the "value" part of the tree storage with a value for each tree
            for (int i = 0; i < mTrees.Count; ++i)
            {
                MutableTuple<Tree, double> it =  mTrees[i];
                tw.setTree(it.Item1);
                it.Item2 = sorter.execute();
            }
            // now sort the list....
            mTrees.Sort(treePairValue);
        }

        public double percentile(int pct)
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
        public void randomize()
        {
            // fill the "value" part of the tree storage with a random value for each tree
            for (int i = 0; i < mTrees.Count; ++i)
            {
                MutableTuple<Tree, double> it = mTrees[i];
                it.Item2 = RandomGenerator.drandom();
            }

            // now sort the list....
            mTrees.Sort(treePairValue);
        }

        private void prepareGrids()
        {
            RectangleF box = ForestManagementEngine.standGrid().boundingBox(mStand.id());
            if (mStandRect == box)
            {
                return;
            }
            mStandRect = box;
            // the memory of the grids is only reallocated if the current box is larger then the previous...
            mStandGrid.setup(box, Constant.cHeightSize);
            mTreeCountGrid.setup(box, Constant.cHeightSize);
            mLocalGrid.setup(box, Constant.cPxSize);
            // mark areas outside of the grid...
            GridRunner<int> runner = new GridRunner<int>(ForestManagementEngine.standGrid().grid(), box);
            for (runner.next(); runner.isValid(); runner.next())
            {
                int p = runner.current();
                if (runner.current() != mStand.id())
                {
                    p = -1;
                }
                runner.setCurrent(++p);
            }
            // copy stand limits to the grid
            for (int iy = 0; iy < mLocalGrid.sizeY(); ++iy)
            {
                for (int ix = 0; ix < mLocalGrid.sizeX(); ++ix)
                {
                    mLocalGrid[ix, iy] = mStandGrid.valueAtIndex(ix / Constant.cPxPerHeight, iy / Constant.cPxPerHeight) == -1.0F ? -1.0F : 0.0F;
                }
            }
        }

        private void runGrid(Action<float, int, Tree, FMTreeList> func)
        {
            if (mStandRect == null)
            {
                prepareGrids();
            }

            // set all values to 0 (within the limits of the stand grid)
            for (int p = 0; p < mStandGrid.count(); ++p)
            {
                if (mStandGrid[p] != -1.0F)
                {
                    mStandGrid[p] = 0.0F;
                }
            }
            mTreeCountGrid.initialize(0);
            int invalid_index = 0;
            foreach (MutableTuple<Tree, double> it in mTrees)
            {
                Tree tree = it.Item1;
                Point p = mStandGrid.indexAt(tree.position());
                if (mStandGrid.isIndexValid(p))
                {
                    func.Invoke(mStandGrid.valueAtIndex(p), mTreeCountGrid.valueAtIndex(p), tree, this);
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
            for (int i = 0; i < mStandGrid.count(); ++i)
            {
                func.Invoke(mStandGrid.valueAtIndex(i), mTreeCountGrid.valueAtIndex(i), null, this);
            }
        }

        public void rungrid_heightmax(float cell, int n, Tree tree, FMTreeList list)
        {
            // Q_UNUSED(n); Q_UNUSED(list);
            if (tree != null)
            {
                cell = Math.Max(cell, tree.height());
            }
        }

        public void rungrid_basalarea(float cell, int n, Tree tree, FMTreeList list)
        {
            // Q_UNUSED(list);
            if (tree != null)
            {
                cell += (float)tree.basalArea();
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

        public void rungrid_volume(float cell, int n, Tree tree, FMTreeList list)
        {
            // Q_UNUSED(list);
            if (tree != null)
            {
                cell += (float)tree.volume();
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

        public void rungrid_custom(float cell, int n, Tree tree, FMTreeList list)
        {
            if (tree != null)
            {
                list.mRunGridCustomCell = cell;
                TreeWrapper tw = new TreeWrapper(tree);
                cell = (float)list.mRunGridCustom.calculate(tw);
                ++n;
            }
        }

        public void prepareStandGrid(string type, string custom_expression)
        {
            if (mStand != null)
            {
                Debug.WriteLine("Error: FMTreeList: no current stand defined.");
                return;
            }

            if (type == "height")
            {
                runGrid(rungrid_heightmax);
            }
            else if (type == "basalArea")
            {
                runGrid(rungrid_basalarea);
            }
            else if (type == "volume")
            {
                runGrid(rungrid_volume);
            }
            else if (type == "custom")
            {
                mRunGridCustom = new Expression(custom_expression);
                mRunGridCustomCell = mRunGridCustom.addVar("cell");
                runGrid(rungrid_custom);
                mRunGridCustom = null;
            }
            else
            {
                Debug.WriteLine("FMTreeList: invalid type for prepareStandGrid: " + type);
            }
        }

        public void exportStandGrid(string file_name)
        {
            file_name = GlobalSettings.instance().path(file_name);
            Helper.saveToTextFile(file_name, Grid.gridToESRIRaster(mStandGrid));
            Debug.WriteLine("saved grid to file " + file_name);
        }

        private void check_locks()
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
