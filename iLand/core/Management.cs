using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace iLand.core
{
    /** @class Management Management executes management routines.
        @ingroup core
        The actual iLand management is based on Javascript functions. This class provides
        the frame for executing the javascript as well as the functions that are called by scripts and
        that really do the work.
        See http://iland.boku.ac.at/iLand+scripting, http://iland.boku.ac.at/Object+Management for management Javascript API.
        */
    internal class Management
    {
        private double mRemoveFoliage;
        private double mRemoveBranch;
        private double mRemoveStem;
        private string mScriptFile;
        private List<MutableTuple<Tree, double>> mTrees;
        private QJSEngine mEngine;
        private int mRemoved;

        string scriptFile() { return mScriptFile; }

        // property getter & setter for removal fractions
        /// removal fraction foliage: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        public double removeFoliage() { return mRemoveFoliage; }
        /// removal fraction branch biomass: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        public double removeBranch() { return mRemoveBranch; }
        /// removal fraction stem biomass: 0: 0% will be removed, 1: 100% will be removed from the forest by management operations (i.e. calls to manage() instead of kill())
        public double removeStem() { return mRemoveStem; }

        public void setRemoveFoliage(double fraction) { mRemoveFoliage = fraction; }
        public void setRemoveBranch(double fraction) { mRemoveBranch = fraction; }
        public void setRemoveStem(double fraction) { mRemoveStem = fraction; }

        public int count() { return mTrees.Count; } ///< return number of trees currently in list
                                                    /// calculate the mean value for all trees in the internal list for 'expression' (filtered by the filter criterion)
        public double mean(string expression, string filter = null) { return aggregate_function(expression, filter, "mean"); }
        /// calculate the sum for all trees in the internal list for the 'expression' (filtered by the filter criterion)
        public double sum(string expression, string filter = null) { return aggregate_function(expression, filter, "sum"); }


        // global output function
        public string executeScript(string cmd = "")
        {
            return GlobalSettings.instance().scriptEngine().executeScript(cmd);
        }

        public Management()
        {
            // setup the scripting engine
            mEngine = GlobalSettings.instance().scriptEngine();
            QJSValue objectValue = mEngine.newQObject(this);
            mEngine.globalObject().setProperty("management", objectValue);

            // default values for removal fractions
            // 100% of the stem, 0% of foliage and branches
            mRemoveFoliage = 0.0;
            mRemoveBranch = 0.0;
            mRemoveStem = 1.0;
        }

        public int loadAll() { return load(null); } ///< load all trees, return number of trees

        public int remain(int number)
        {
            Debug.WriteLine("remain called (number): " + number);
            Model m = GlobalSettings.instance().model();
            AllTreeIterator at = new AllTreeIterator(m);
            List<Tree> trees = new List<Tree>();
            for (Tree t = at.next(); t != null; t = at.next())
            {
                trees.Add(t);
            }
            int to_kill = trees.Count - number;
            Debug.WriteLine(trees.Count + " standing, targetsize " + number + ", hence " + to_kill + " trees to remove");
            for (int i = 0; i < to_kill; i++)
            {
                int index = RandomGenerator.irandom(0, trees.Count);
                trees[index].remove();
                trees.RemoveAt(index);
            }
            mRemoved += to_kill;
            return to_kill;
        }

        public int killAll()
        {
            int c = mTrees.Count;
            for (int i = 0; i < mTrees.Count; i++)
                mTrees[i].Item1.remove();
            mTrees.Clear();
            return c;
        }

        public int disturbanceKill()
        {
            int c = mTrees.Count;
            for (int i = 0; i < mTrees.Count; i++)
            {
                mTrees[i].Item1.removeDisturbance(0.1, 0.1, 0.1, 0.1, 1.0);
            }
            mTrees.Clear();
            return c;
        }

        public int kill(string filter, double fraction)
        {
            return remove_trees(filter, fraction, false);
        }

        public int manage(string filter, double fraction)
        {
            return remove_trees(filter, fraction, true);
        }

        public void cutAndDrop()
        {
            //int c = mTrees.Count;
            for (int i = 0; i < mTrees.Count; i++)
            {
                mTrees[i].Item1.setDeathCutdown(); // set flag that tree is cut down
                mTrees[i].Item1.die();
            }
            mTrees.Clear();
        }

        private int remove_percentiles(int pctfrom, int pctto, int number, bool management)
        {
            if (mTrees.Count == 0)
            {
                return 0;
            }
            int index_from = Global.limit((int)(pctfrom / 100.0 * mTrees.Count), 0, mTrees.Count);
            int index_to = Global.limit((int)(pctto / 100.0 * mTrees.Count), 0, mTrees.Count);
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
                        mTrees[i].Item1.remove(removeFoliage(), removeBranch(), removeStem());
                    }
                }
                else
                {
                    // just kill...
                    for (int i = index_from; i < index_to; i++)
                    {
                        mTrees[i].Item1.remove();
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
                    if (mTrees[rnd_index].Item1.isDead())
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
                        mTrees[rnd_index].Item1.remove(removeFoliage(), removeBranch(), removeStem());
                    }
                    else
                    {
                        mTrees[rnd_index].Item1.remove();
                    }
                }
            }
            Debug.WriteLine(count + " removed.");
            // clean up the tree list...
            for (int i = mTrees.Count - 1; i >= 0; --i)
            {
                if (mTrees[i].Item1.isDead())
                {
                    mTrees.RemoveAt(i);
                }
            }
            return count; // killed or manages
        }

        /** remove trees from a list and reduce the list.
          */
        private int remove_trees(string expression, double fraction, bool management)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression expr = new Expression(expression, tw);
            expr.enableIncSum();
            int n = 0;
            for (int tp = 0; tp < mTrees.Count; ++tp)
            {
                tw.setTree(mTrees[tp].Item1);
                // if expression evaluates to true and if random number below threshold...
                if (expr.calculate(tw) != 0.0 && RandomGenerator.drandom() <= fraction)
                {
                    // remove from system
                    if (management)
                    {
                        mTrees[tp].Item1.remove(removeFoliage(), removeBranch(), removeStem()); // management with removal fractions
                    }
                    else
                    {
                        mTrees[tp].Item1.remove(); // kill
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
        private double aggregate_function(string expression, string filter, string type)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression expr = new Expression(expression, tw);

            double sum = 0.0;
            int n = 0;
            if (String.IsNullOrEmpty(filter))
            {
                // without filtering
                for (int tp = 0; tp < mTrees.Count; ++tp)
                {
                    tw.setTree(mTrees[tp].Item1);
                    sum += expr.calculate();
                    ++n;
                    ++tp;
                }
            }
            else
            {
                // with filtering
                Expression filter_expr = new Expression(filter, tw);
                filter_expr.enableIncSum();
                for (int tp = 0; tp < mTrees.Count; ++tp)
                {
                    tw.setTree(mTrees[tp].Item1);
                    if (filter_expr.calculate() != 0.0)
                    {
                        sum += expr.calculate();
                        ++n;
                    }
                    ++tp;
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

        // introduced with switch to QJSEngine (context.throwMessage not available any more)
        private void throwError(string errormessage)
        {
            GlobalSettings.instance().scriptEngine().evaluate(String.Format("throw '{0}'", errormessage));
            Debug.WriteLine("Management-script error: " + errormessage);
            // no idea if this works!!!
        }

        // from the range percentile range pctfrom to pctto (each 1..100)
        public int killPct(int pctfrom, int pctto, int number)
        {
            return remove_percentiles(pctfrom, pctto, number, false);
        }

        // from the range percentile range pctfrom to pctto (each 1..100)
        public int managePct(int pctfrom, int pctto, int number)
        {
            return remove_percentiles(pctfrom, pctto, number, true);
        }

        public int manageAll()
        {
            int c = mTrees.Count;
            for (int i = 0; i < mTrees.Count; i++)
            {
                mTrees[i].Item1.remove(removeFoliage(),
                                       removeBranch(),
                                       removeStem()); // remove with current removal fractions
            }
            mTrees.Clear();
            return c;
        }

        public void run()
        {
            mTrees.Clear();
            mRemoved = 0;
            Debug.WriteLine("run() called");
            QJSValue mgmt = mEngine.globalObject().property("manage");
            int year = GlobalSettings.instance().currentYear();
            //mgmt.call(QJSValue(), QScriptValueList()+year);
            QJSValue result = mgmt.call(new List<QJSValue>() { new QJSValue(year) });
            if (result.isError())
            {
                Debug.WriteLine("Script Error occured: " + result.toString());//  + "\n" + mEngine.uncaughtExceptionBacktrace();
            }
        }

        public void loadScript(string fileName)
        {
            mScriptFile = fileName;
            GlobalSettings.instance().scriptEngine().loadScript(fileName);
        }

        public int filterIdList(List<object> idList)
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
                if (!ids.Contains(mTrees[tp].Item1.id()))
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

        public int filter(string filter)
        {
            TreeWrapper tw = new TreeWrapper();
            Expression expr = new Expression(filter, tw);
            expr.enableIncSum();
            int n_before = mTrees.Count;
            for (int tp = 0; tp < mTrees.Count; ++tp)
            {
                tw.setTree(mTrees[tp].Item1);
                double value = expr.calculate(tw);
                // keep if expression returns true (1)
                bool keep = value == 1.0;
                // if value is >0 (i.e. not "false"), then draw a random number
                if (!keep && value > 0.0)
                {
                    keep = RandomGenerator.drandom() < value;
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

        public int loadResourceUnit(int ruindex)
        {
            Model m = GlobalSettings.instance().model();
            ResourceUnit ru = m.ru(ruindex);
            if (ru == null)
            {
                return -1;
            }
            mTrees.Clear();
            for (int i = 0; i < ru.trees().Count; i++)
            {
                if (!ru.tree(i).isDead())
                {
                    mTrees.Add(new MutableTuple<Tree, double>(ru.tree(i), 0.0));
                }
            }
            return mTrees.Count;
        }

        public int load(string filter)
        {
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
                    if (!t.isDead() && expr.execute() == 0.0)
                    {
                        mTrees.Add(new MutableTuple<Tree, double>(t, 0.0));
                    }
                }
            }
            return mTrees.Count;
        }

        public void loadFromTreeList(List<Tree> tree_list)
        {
            mTrees.Clear();
            for (int i = 0; i < tree_list.Count; ++i)
            {
                mTrees.Add(new MutableTuple<Tree, double>(tree_list[i], 0.0));
            }
        }

        // loadFromMap: script access
        public void loadFromMap(MapGridWrapper wrap, int key)
        {
            if (wrap == null)
            {
                throwError("loadFromMap called with invalid map object!");
                return;
            }
            loadFromMap(wrap.map(), key);
        }

        public void killSaplings(MapGridWrapper wrap, int key)
        {
            //MapGridWrapper *wrap = qobject_cast<MapGridWrapper*>(map_grid_object.toQObject());
            //if (!wrap) {
            //    context().throwError("loadFromMap called with invalid map object!");
            //    return;
            //}
            //loadFromMap(wrap.map(), key);
            RectangleF box = wrap.map().boundingBox(key);
            GridRunner<float> runner = new GridRunner<float>(GlobalSettings.instance().model().grid(), box);
            for (runner.next(); runner.isValid(); runner.next())
            {
                if (wrap.map().standIDFromLIFCoord(runner.currentIndex()) == key)
                {
                    ResourceUnit ru = null;
                    SaplingCell sc = GlobalSettings.instance().model().saplings().cell(runner.currentIndex(), true, ref ru);
                    if (sc != null)
                    {
                        GlobalSettings.instance().model().saplings().clearSaplings(sc, ru, true);
                    }
                }
            }
        }

        /// specify removal fractions
        /// @param SWDFrac 0: no change, 1: remove all of standing woody debris
        /// @param DWDfrac 0: no change, 1: remove all of downled woody debris
        /// @param litterFrac 0: no change, 1: remove all of soil litter
        /// @param soilFrac 0: no change, 1: remove all of soil organic matter
        public void removeSoilCarbon(MapGridWrapper wrap, int key, double SWDfrac, double DWDfrac, double litterFrac, double soilFrac)
        {
            if (!(SWDfrac >= 0.0 && SWDfrac <= 1.0 && DWDfrac >= 0.0 && DWDfrac <= 1.0 && soilFrac >= 0.0 && soilFrac <= 1.0 && litterFrac >= 0.0 && litterFrac <= 1.0))
            {
                throwError("removeSoilCarbon called with invalid parameters!!\nArgs: ---");
                return;
            }
            List<MutableTuple<ResourceUnit, double>> ru_areas = wrap.map().resourceUnitAreas(key).ToList();
            double total_area = 0.0;
            for (int i = 0; i < ru_areas.Count; ++i)
            {
                ResourceUnit ru = ru_areas[i].Item1;
                double area_factor = ru_areas[i].Item2; // 0..1
                total_area += area_factor;
                // swd
                if (SWDfrac > 0.0)
                {
                    ru.snag().removeCarbon(SWDfrac * area_factor);
                }
                // soil pools
                ru.soil().disturbance(DWDfrac * area_factor, litterFrac * area_factor, soilFrac * area_factor);
                // Debug.WriteLine(ru.index() + area_factor;
            }
            Debug.WriteLine("total area " + total_area + " of " + wrap.map().area(key));
        }

        /** slash snags (SWD and otherWood-Pools) of polygon \p key on the map \p wrap.
          The factor is scaled to the overlapping area of \p key on the resource unit.
          @param wrap MapGrid to use together with \p key
          @param key ID of the polygon.
          @param slash_fraction 0: no change, 1: 100%
           */
        public void slashSnags(MapGridWrapper wrap, int key, double slash_fraction)
        {
            if (slash_fraction < 0 || slash_fraction > 1)
            {
                throwError(String.Format("slashSnags called with invalid parameters!!\nArgs: ...."));
                return;
            }
            List<MutableTuple<ResourceUnit, double>> ru_areas = wrap.map().resourceUnitAreas(key).ToList();
            double total_area = 0.0;
            for (int i = 0; i < ru_areas.Count; ++i)
            {
                ResourceUnit ru = ru_areas[i].Item1;
                double area_factor = ru_areas[i].Item2; // 0..1
                total_area += area_factor;
                ru.snag().management(slash_fraction * area_factor);
                // Debug.WriteLine(ru.index() + area_factor;
            }
            Debug.WriteLine("total area " + total_area + " of " + wrap.map().area(key));

        }

        /** loadFromMap selects trees located on pixels with value 'key' within the grid 'map_grid'.
            */
        public void loadFromMap(MapGrid map_grid, int key)
        {
            if (map_grid == null)
            {
                Debug.WriteLine("invalid parameter for loadFromMap: Map expected!");
                return;
            }
            if (map_grid.isValid())
            {
                List<Tree> tree_list = map_grid.trees(key);
                loadFromTreeList(tree_list);
            }
            else
            {
                Debug.WriteLine("loadFromMap: grid is not valid - no trees loaded");
            }

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
                tw.setTree(mTrees[i].Item1);
                MutableTuple<Tree, double> tree = mTrees[i];
                tree.Item2 = sorter.execute();
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
                MutableTuple<Tree, double> tree = mTrees[i];
                tree.Item2 = RandomGenerator.drandom();
            }
            // now sort the list....
            mTrees.Sort(treePairValue);
        }
    }
}
