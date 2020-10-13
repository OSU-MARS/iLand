using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace iLand.Core
{
    /** @class Management Management executes management routines.
        @ingroup core
        The actual iLand management is based on Javascript functions. This class provides
        the frame for executing the javascript as well as the functions that are called by scripts and
        that really do the work.
        See http://iland.boku.ac.at/iLand+scripting, http://iland.boku.ac.at/Object+Management for management Javascript API.
        */
    public class Management
    {
        private readonly List<MutableTuple<Tree, double>> mTrees;

        // property getter & setter for removal fractions
        /// removal fraction foliage: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        private readonly double mRemoveFoliage;
        /// removal fraction branch biomass: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        private readonly double mRemoveBranch;
        /// removal fraction stem biomass: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        private readonly double mRemoveStem;

        public Management()
        {
            // default values for removal fractions
            // 100% of the stem, 0% of foliage and branches
            this.mRemoveFoliage = 0.0;
            this.mRemoveBranch = 0.0;
            this.mRemoveStem = 1.0;
            this.mTrees = new List<MutableTuple<Tree, double>>();
        }

        ///< return number of trees currently in list
        public int Count() { return mTrees.Count; }
        /// calculate the mean value for all trees in the internal list for 'expression' (filtered by the filter criterion)
        // public double Mean(string expression, string filter = null) { return AggregateFunction(expression, filter, "mean"); }
        /// calculate the sum for all trees in the internal list for the 'expression' (filtered by the filter criterion)
        // public double Sum(string expression, string filter = null) { return AggregateFunction(expression, filter, "sum"); }

        ///< load all trees, return number of trees
        //public int LoadAll()
        //{ 
        //    return Load(null);
        //}

        public int Remain(int number, Model model)
        {
            Debug.WriteLine("remain called (number): " + number);
            AllTreeIterator at = new AllTreeIterator(model);
            List<Tree> trees = new List<Tree>();
            for (Tree t = at.MoveNext(); t != null; t = at.MoveNext())
            {
                trees.Add(t);
            }
            int to_kill = trees.Count - number;
            Debug.WriteLine(trees.Count + " standing, targetsize " + number + ", hence " + to_kill + " trees to remove");
            for (int i = 0; i < to_kill; i++)
            {
                int index = model.RandomGenerator.Random(0, trees.Count);
                trees[index].Remove(model);
                trees.RemoveAt(index);
            }
            return to_kill;
        }

        public int KillAll(Model model)
        {
            int c = mTrees.Count;
            for (int i = 0; i < mTrees.Count; i++)
            {
                mTrees[i].Item1.Remove(model);
            }
            mTrees.Clear();
            return c;
        }

        public int DisturbanceKill(Model model)
        {
            int c = mTrees.Count;
            for (int i = 0; i < mTrees.Count; i++)
            {
                mTrees[i].Item1.RemoveDisturbance(model, 0.1, 0.1, 0.1, 0.1, 1.0);
            }
            mTrees.Clear();
            return c;
        }

        public int Kill(Model model, string filter, double fraction)
        {
            return RemoveTrees(model, filter, fraction, false);
        }

        public int Manage(Model model, string filter, double fraction)
        {
            return RemoveTrees(model, filter, fraction, true);
        }

        public void CutAndDrop(Model model)
        {
            //int c = mTrees.Count;
            for (int i = 0; i < mTrees.Count; i++)
            {
                mTrees[i].Item1.SetDeathReasonCutdown(); // set flag that tree is cut down
                mTrees[i].Item1.Die(model);
            }
            mTrees.Clear();
        }

        private int RemovePercentiles(Model model, int pctfrom, int pctto, int number, bool management)
        {
            if (mTrees.Count == 0)
            {
                return 0;
            }
            int index_from = Global.Limit((int)(pctfrom / 100.0 * mTrees.Count), 0, mTrees.Count);
            int index_to = Global.Limit((int)(pctto / 100.0 * mTrees.Count), 0, mTrees.Count);
            if (index_from >= index_to)
            {
                return 0;
            }
            Debug.WriteLine("attempting to remove " + number + " trees between indices " + index_from + " and " + index_to);
            int count = number;
            if (index_to - index_from <= number)
            {
                // kill all
                if (management)
                {
                    // management
                    for (int i = index_from; i < index_to; i++)
                    {
                        mTrees[i].Item1.Remove(model, mRemoveFoliage, mRemoveBranch, mRemoveStem);
                    }
                }
                else
                {
                    // just kill...
                    for (int i = index_from; i < index_to; i++)
                    {
                        mTrees[i].Item1.Remove(model);
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
                    int rnd_index = model.RandomGenerator.Random(index_from, index_to);
                    if (mTrees[rnd_index].Item1.IsDead())
                    {
                        if (--cancel < 0)
                        {
                            Debug.WriteLine("kill: canceling search. " + number + " trees left.");
                            count -= number; // not all trees were killed
                            break;
                        }
                        continue;
                    }
                    cancel = 1000;
                    number--;
                    if (management)
                    {
                        mTrees[rnd_index].Item1.Remove(model, mRemoveFoliage, mRemoveBranch, mRemoveStem);
                    }
                    else
                    {
                        mTrees[rnd_index].Item1.Remove(model);
                    }
                }
            }
            Debug.WriteLine(count + " removed.");
            // clean up the tree list...
            for (int i = mTrees.Count - 1; i >= 0; --i)
            {
                if (mTrees[i].Item1.IsDead())
                {
                    mTrees.RemoveAt(i);
                }
            }
            return count; // killed or manages
        }

        /** remove trees from a list and reduce the list.
          */
        private int RemoveTrees(Model model, string expression, double fraction, bool management)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression expr = new Expression(expression, tw);
            expr.EnableIncrementalSum();
            int n = 0;
            for (int tp = 0; tp < mTrees.Count; ++tp)
            {
                tw.Tree = mTrees[tp].Item1;
                // if expression evaluates to true and if random number below threshold...
                if (expr.Calculate(tw, model) != 0.0 && model.RandomGenerator.Random() <= fraction)
                {
                    // remove from system
                    if (management)
                    {
                        mTrees[tp].Item1.Remove(model, mRemoveFoliage, mRemoveBranch, mRemoveStem); // management with removal fractions
                    }
                    else
                    {
                        mTrees[tp].Item1.Remove(model); // kill
                    }

                    // remove from tree list
                    mTrees.RemoveAt(tp);
                    --tp;
                    n++;
                }
                else
                {
                    ++tp;
                }
            }
            return n;
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

        // from the range percentile range pctfrom to pctto (each 1..100)
        public int KillPercentage(Model model, int pctfrom, int pctto, int number)
        {
            return RemovePercentiles(model, pctfrom, pctto, number, false);
        }

        // from the range percentile range pctfrom to pctto (each 1..100)
        public int ManagePercentage(Model model, int pctfrom, int pctto, int number)
        {
            return RemovePercentiles(model, pctfrom, pctto, number, true);
        }

        public int ManageAll(Model model)
        {
            int c = mTrees.Count;
            for (int i = 0; i < mTrees.Count; i++)
            {
                mTrees[i].Item1.Remove(model, mRemoveFoliage, mRemoveBranch, mRemoveStem);
            }
            mTrees.Clear();
            return c;
        }

        public void Run()
        {
            mTrees.Clear();
            Debug.WriteLine("run() called");
        }

        public int FilterIDList(List<object> idList)
        {
            List<int> ids = new List<int>();
            foreach (object v in idList)
            {
                if (v != null)
                {
                    ids.Add((int)v);
                }
            }
            //    QHash<int, int> ids;
            //    foreach(object v, idList)
            //        ids[v.toInt()] = 1;
            for (int tp = 0; tp < mTrees.Count; ++tp)
            {
                if (!ids.Contains(mTrees[tp].Item1.ID))
                {
                    mTrees.RemoveAt(tp);
                    --tp;
                }
                else
                {
                    ++tp;
                }
            }
            Debug.WriteLine("filter by id-list: " + mTrees.Count);
            return mTrees.Count;
        }

        public int Filter(Model model, string filter)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression expr = new Expression(filter, tw);
            expr.EnableIncrementalSum();
            int n_before = mTrees.Count;
            for (int tp = 0; tp < mTrees.Count; ++tp)
            {
                tw.Tree = mTrees[tp].Item1;
                double value = expr.Calculate(tw, model);
                // keep if expression returns true (1)
                bool keep = value == 1.0;
                // if value is >0 (i.e. not "false"), then draw a random number
                if (!keep && value > 0.0)
                {
                    keep = model.RandomGenerator.Random() < value;
                }
                if (!keep)
                {
                    mTrees.RemoveAt(tp);
                    --tp;
                }
                else
                {
                    ++tp;
                }
            }

            Debug.WriteLine("filtering with " + filter + " N=" + n_before + "/" + mTrees.Count + " trees (before/after filtering).");
            return mTrees.Count;
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

        public void LoadFromTreeList(List<Tree> tree_list)
        {
            mTrees.Clear();
            for (int i = 0; i < tree_list.Count; ++i)
            {
                mTrees.Add(new MutableTuple<Tree, double>(tree_list[i], 0.0));
            }
        }

        // loadFromMap: script access
        public void LoadFromMap(MapGridWrapper wrap, int key)
        {
            if (wrap == null)
            {
                throw new ArgumentNullException(nameof(wrap));
            }
            LoadFromMap(wrap.StandGrid, key);
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
                if (wrap.StandGrid.StandIDFromLifCoord(runner.CurrentIndex()) == key)
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
        public void RemoveSoilCarbon(MapGridWrapper wrap, int key, double SWDfrac, double DWDfrac, double litterFrac, double soilFrac)
        {
            if (!(SWDfrac >= 0.0 && SWDfrac <= 1.0 && DWDfrac >= 0.0 && DWDfrac <= 1.0 && soilFrac >= 0.0 && soilFrac <= 1.0 && litterFrac >= 0.0 && litterFrac <= 1.0))
            {
                throw new ArgumentException("removeSoilCarbon called with invalid parameters!!");
            }
            List<MutableTuple<ResourceUnit, double>> ru_areas = wrap.StandGrid.ResourceUnitAreas(key).ToList();
            double total_area = 0.0;
            for (int i = 0; i < ru_areas.Count; ++i)
            {
                ResourceUnit ru = ru_areas[i].Item1;
                double area_factor = ru_areas[i].Item2; // 0..1
                total_area += area_factor;
                // swd
                if (SWDfrac > 0.0)
                {
                    ru.Snags.RemoveCarbon(SWDfrac * area_factor);
                }
                // soil pools
                ru.Soil.Disturbance(DWDfrac * area_factor, litterFrac * area_factor, soilFrac * area_factor);
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
        public void SlashSnags(MapGridWrapper wrap, int key, double slash_fraction)
        {
            if (slash_fraction < 0.0 || slash_fraction > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(slash_fraction), "slashSnags called with invalid parameters!");
            }
            List<MutableTuple<ResourceUnit, double>> ru_areas = wrap.StandGrid.ResourceUnitAreas(key).ToList();
            double total_area = 0.0;
            for (int i = 0; i < ru_areas.Count; ++i)
            {
                ResourceUnit ru = ru_areas[i].Item1;
                double area_factor = ru_areas[i].Item2; // 0..1
                total_area += area_factor;
                ru.Snags.Management(slash_fraction * area_factor);
                // Debug.WriteLine(ru.index() + area_factor;
            }
            Debug.WriteLine("total area " + total_area + " of " + wrap.StandGrid.Area(key));
        }

        /** loadFromMap selects trees located on pixels with value 'key' within the grid 'map_grid'.
            */
        public void LoadFromMap(MapGrid map_grid, int key)
        {
            if (map_grid == null)
            {
                Debug.WriteLine("invalid parameter for loadFromMap: Map expected!");
                return;
            }
            if (map_grid.IsValid())
            {
                List<Tree> tree_list = map_grid.Trees(key);
                LoadFromTreeList(tree_list);
            }
            else
            {
                Debug.WriteLine("loadFromMap: grid is not valid - no trees loaded");
            }
        }

        private int TreePairValue(MutableTuple<Tree, double> p1, MutableTuple<Tree, double> p2)
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

        public void Sort(Model model, string statement)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression sorter = new Expression(statement, tw);
            // fill the "value" part of the tree storage with a value for each tree
            for (int i = 0; i < mTrees.Count; ++i)
            {
                tw.Tree = mTrees[i].Item1;
                MutableTuple<Tree, double> tree = mTrees[i];
                tree.Item2 = sorter.Execute(model);
            }
            // now sort the list....
            mTrees.Sort(TreePairValue);
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
        public void Randomize(Model model)
        {
            // fill the "value" part of the tree storage with a random value for each tree
            for (int i = 0; i < mTrees.Count; ++i)
            {
                MutableTuple<Tree, double> tree = mTrees[i];
                tree.Item2 = model.RandomGenerator.Random();
            }
            // now sort the list....
            mTrees.Sort(TreePairValue);
        }
    }
}
