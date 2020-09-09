using iLand.core;
using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.abe
{
    /** @class ActThinning
        @ingroup abe
        The ActThinning class implements a very general interface to thinning activties.
        */
    internal class ActThinning : Activity
    {
        private struct SCustomThinning
        {
            public string filter; ///< additional filter
            public bool usePercentiles; ///< if true, classes relate to percentiles, if 'false' classes relate to relative dbh classes
            public bool removal; ///< if true, classes define removals, if false trees should *stay* in the class
            public bool relative; ///< if true, values are per cents, if false, values are absolute values per hectare
            public double targetValue; ///< the number (per ha) that should be removed, see targetVariable
            public bool targetRelative; ///< if true, the target variable is relative to the stock, if false it is absolute
            public string targetVariable; ///< target variable ('volume', 'basalArea', 'stems') / ha
            public List<double> classValues; ///< class values (the number of values defines the number of classes)
            public List<int> classPercentiles; ///< percentiles [0..100] for the classes (count = count(classValues) + 1
            public double minDbh; ///< only trees with dbh > minDbh are considered (default: 0)
            public int remainingStems; ///< minimum remaining stems/ha (>minDbh)
        };

        private struct SSelectiveThinning
        {
            public int N; // stems per ha target
        };

        private SSelectiveThinning mSelectiveThinning;

        private List<SCustomThinning> mCustomThinnings;

        private Dictionary<Species, double> mSpeciesSelectivity;
        private ThinningType mThinningType;

        // syntax checking
        private static List<string> mSyntaxCustom;
        private static List<string> mSyntaxSelective;


        public ActThinning(FMSTP parent)
            : base(parent)
        {
            mBaseActivity.setIsScheduled(true); // use the scheduler
            mBaseActivity.setDoSimulate(true); // simulate per default
            mThinningType = ThinningType.Invalid;
            if (mSyntaxCustom.Count == 0)
            {
                mSyntaxCustom = new List<string>(Activity.mAllowedProperties) { "percentile", "removal", "thinning",
                                                                                "relative", "remainingStems", "minDbh",
                                                                                "filter", "targetVariable", "targetRelative",
                                                                                "targetValue", "classes", "onEvaluate" };
            }
        }

        public override string type()
        {
            string th;
            switch (mThinningType)
            {
                case ThinningType.Invalid: th = "Invalid"; break;
                case ThinningType.FromBelow: th = "from below"; break;
                case ThinningType.FromAbove: th = "from above"; break;
                case ThinningType.Custom: th = "custom"; break;
                case ThinningType.Selection: th = "selection"; break;
                default: throw new NotSupportedException();
            }

            return String.Format("thinning ({0})", th);
        }

        public new void setup(QJSValue value)
        {
            base.setup(value); // setup base events
            mThinningType = ThinningType.Invalid;
            string th_type = FMSTP.valueFromJs(value, "thinning").toString();
            if (th_type == "fromBelow")
            {
                mThinningType = ThinningType.FromBelow;
            }
            else if (th_type == "fromAbove")
            {
                mThinningType = ThinningType.FromAbove;
            }
            else if (th_type == "custom")
            {
                mThinningType = ThinningType.Custom;
            }
            else if (th_type == "selection")
            {
                mThinningType = ThinningType.Selection;
            }
            else
            {
                throw new NotSupportedException(String.Format("Setup of thinning: invalid thinning type: {0}", th_type));
            }

            switch (mThinningType)
            {
                case ThinningType.Custom: setupCustom(value); break;
                case ThinningType.Selection: setupSelective(value); break;
                default: throw new NotSupportedException("No setup defined for thinning type");
            }
        }

        public override bool evaluate(FMStand stand)
        {
            bool return_value = true;
            switch (mThinningType)
            {
                case ThinningType.Custom:
                    for (int i = 0; i < mCustomThinnings.Count; ++i)
                    {
                        return_value = return_value && evaluateCustom(stand, mCustomThinnings[i]);
                    }
                    return return_value; // false if one fails
                case ThinningType.Selection:
                    return evaluateSelective(stand);
                default:
                    throw new NotSupportedException("evaluate: not available for thinning type");
            }
        }

        public override bool execute(FMStand stand)
        {
            if (stand.trace()) Debug.WriteLine(stand.context() + " execute activity " + name() + ": " + type());
            if (events().hasEvent("onExecute"))
            {
                // switch off simulation mode
                stand.currentFlags().setDoSimulate(false);
                // execute this event
                bool result = base.execute(stand);
                stand.currentFlags().setDoSimulate(true);
                return result;
            }
            else
            {
                // default behavior: process all marked trees (harvest / cut)
                if (stand.trace())
                {
                    Debug.WriteLine(stand.context() + " activity " + name() + " remove all marked trees.");
                }
                FMTreeList trees = new FMTreeList(stand);
                trees.removeMarkedTrees();
                return true;
            }
        }

        private void setupCustom(QJSValue value)
        {
            events().setup(value, new List<string>() { "onEvaluate" });
            mCustomThinnings.Clear();
            if (value.hasProperty("thinnings") && value.property("thinnings").isArray())
            {
                QJSValueIterator it = new QJSValueIterator(value.property("thinnings"));
                while (it.hasNext())
                {
                    it.next();
                    if (it.name() == "length")
                    {
                        continue;
                    }
                    SCustomThinning thinning = new SCustomThinning();
                    mCustomThinnings.Add(thinning);
                    setupSingleCustom(it.value(), thinning);
                }
            }
            else
            {
                SCustomThinning thinning = new SCustomThinning();
                mCustomThinnings.Add(thinning);
                setupSingleCustom(value, thinning);
            }
        }

        private void setupSelective(QJSValue value)
        {
            mSelectiveThinning.N = FMSTP.valueFromJs(value, "N", "400").toInt();
        }

        // setup of the "custom" thinning operation
        private void setupSingleCustom(QJSValue value, SCustomThinning custom)
        {
            FMSTP.checkObjectProperties(value, mSyntaxCustom, "setup of 'custom' thinning:" + name());

            custom.usePercentiles = FMSTP.boolValueFromJs(value, "percentile", true);
            custom.removal = FMSTP.boolValueFromJs(value, "removal", true);
            custom.relative = FMSTP.boolValueFromJs(value, "relative", true);
            custom.remainingStems = FMSTP.valueFromJs(value, "remainingStems", "0").toInt();
            custom.minDbh = FMSTP.valueFromJs(value, "minDbh", "0").toNumber();
            QJSValue filter = FMSTP.valueFromJs(value, "filter", "");
            if (filter.isString())
            {
                custom.filter = filter.toString();
            }
            else
            {
                custom.filter = null;
            }
            custom.targetVariable = FMSTP.valueFromJs(value, "targetVariable", "stems").toString();
            if (custom.targetVariable != "stems" && custom.targetVariable != "basalArea" && custom.targetVariable != "volume")
            {
                throw new NotSupportedException(String.Format("setup of custom Activity: invalid targetVariable: {0}", custom.targetVariable));
            }
            custom.targetRelative = FMSTP.boolValueFromJs(value, "targetRelative", true);
            custom.targetValue = FMSTP.valueFromJs(value, "targetValue", "30").toNumber();
            if (custom.targetRelative && (custom.targetValue > 100.0 || custom.targetValue < 0.0))
            {
                throw new NotSupportedException(String.Format("setup of custom Activity: invalid relative targetValue (0-100): {0}", custom.targetValue));
            }

            QJSValue values = FMSTP.valueFromJs(value, "classes", "", "setup custom acitvity");
            if (!values.isArray())
            {
                throw new NotSupportedException("setup of custom activity: the 'classes' is not an array.");
            }
            custom.classValues.Clear();
            custom.classPercentiles.Clear();
            QJSValueIterator it = new QJSValueIterator(values);
            while (it.hasNext())
            {
                it.next();
                if (it.name() == "length")
                {
                    continue;
                }
                custom.classValues.Add(it.value().toNumber());
            }
            if (custom.classValues.Count == 0)
            {
                throw new NotSupportedException("setup of custom thinnings: 'classes' has no elements.");
            }

            // check if sum is 100 for relative classes
            if (custom.relative)
            {
                double sum = 0.0;
                for (int i = 0; i < custom.classValues.Count; ++i)
                {
                    sum += custom.classValues[i];
                }
                if (Math.Abs(sum - 100.0) > 0.000001)
                {
                    throw new NotSupportedException("setup of custom thinnings: 'classes' do not add up to 100 (relative=true).");
                }
            }

            // span the range between 0..100: from e.g. 10,20,30,20,20 . 0,10,30,60,80,100
            double f = 100.0 / custom.classValues.Count;
            double p = 0.0;
            for (int i = 0; i < custom.classValues.Count; ++i, p += f)
            {
                custom.classPercentiles.Add((int)Math.Round(p));
            }
            custom.classPercentiles.Add(100);
        }

        private bool evaluateCustom(FMStand stand, SCustomThinning custom)
        {
            // fire onEvaluate event and collect probabilities
            QJSValue eval_result = events().run("onEvaluate", stand);
            if (eval_result.isBool() && eval_result.toBool() == false)
            {
                return false; // do nothing
            }
            bool species_selective = false;

            if (eval_result.isObject())
            {
                // expecting a list of probabilities....
                // create list if not present
                if (mSpeciesSelectivity.Count == 0)
                {
                    foreach (Species s in GlobalSettings.instance().model().speciesSet().activeSpecies())
                    {
                        mSpeciesSelectivity[s] = 1.0;
                    }
                }
                // fetch from javascript
                double rest_val = eval_result.property("rest").isNumber() ? eval_result.property("rest").toNumber() : 1.0;
                foreach (Species s in mSpeciesSelectivity.Keys)
                {
                    mSpeciesSelectivity[s] = Global.limit(eval_result.property(s.id()).isNumber() ? eval_result.property(s.id()).toNumber() : rest_val, 0.0, 1.0);
                }
                species_selective = true;
            }

            FMTreeList trees = new FMTreeList(stand);
            string filter = custom.filter;
            if (custom.minDbh > 0.0)
            {
                if (String.IsNullOrEmpty(filter) == false)
                {
                    filter += " and ";
                }
                filter += String.Format("dbh>{0}", custom.minDbh);
            }

            if (String.IsNullOrEmpty(filter) == false)
            {
                trees.load(filter);
            }
            else
            {
                trees.loadAll();
            }

            if (custom.remainingStems > 0 && custom.remainingStems >= trees.trees().Count)
            {
                return false;
            }

            if (trees.trees().Count == 0)
            {
                return false;
            }

            // remove harvest flags.
            clearTreeMarks(trees);

            // sort always by target variable (if it is stems, then simply by dbh)
            bool target_dbh = custom.targetVariable == "stems";
            if (target_dbh)
            {
                trees.sort("dbh");
            }
            else
            {
                trees.sort(custom.targetVariable);
            }

            // count trees and values (e.g. volume) in the defined classes
            List<double> values = custom.classValues; // make a copy
            List<double> tree_counts = custom.classValues; // make a copy
            List<int> percentiles = custom.classPercentiles; // make a copy

            for (int i = 0; i < values.Count; ++i)
            {
                tree_counts[i] = 0.0;
            }
            int class_index = 0;
            int n = 0;

            percentiles[0] = 0;
            double tree_count = trees.trees().Count;
            double total_value = 0.0;
            foreach (MutableTuple<Tree, double> it in trees.trees())
            {
                if (n / tree_count * 100.0 > custom.classPercentiles[class_index + 1])
                {
                    ++class_index;
                    percentiles[class_index] = n; // then n'th tree
                }
                tree_counts[class_index]++;
                total_value += target_dbh ? 1.0 : it.Item2; // e.g., sum of volume in the class, or simply count
                ++n;
            }
            while (++class_index < percentiles.Count)
            {
                percentiles[class_index] = n + 1;
            }

            double target_value = 0.0;
            if (custom.targetRelative)
            {
                target_value = custom.targetValue * total_value / 100.0;
            }
            else
            {
                target_value = custom.targetValue * stand.area();
            }

            if (!custom.relative)
            {
                // TODO: does not work now!!! redo!!
                // class values are given in absolute terms, e.g. 40m3/ha.
                // this needs to be translated to relative classes.
                // if the demand in a class cannot be met (e.g. planned removal of 40m3/ha, but there are only 20m3/ha in the class),
                // then the "miss" is distributed to the other classes (via the scaling below).
                for (int i = 0; i < values.Count; ++i)
                {
                    if (values[i] > 0)
                    {
                        if (values[i] <= custom.classValues[i] * stand.area())
                        {
                            values[i] = 1.0; // take all from the class
                        }
                        else
                        {
                            values[i] = custom.classValues[i] * stand.area() / values[i];
                        }
                    }
                }
                // scale to 100
                double sum = 0.0;
                for (int i = 0; i < values.Count; ++i)
                {
                    sum += values[i];
                }
                if (sum > 0.0)
                {
                    for (int i = 0; i < values.Count; ++i)
                    {
                        values[i] *= 100.0 / sum;
                    }
                }
            }

            // *****************************************************************
            // ***************    Main loop
            // *****************************************************************
            for (int i = 0; i < values.Count; ++i)
            {
                values[i] = 0;
            }

            bool finished = false;
            int cls;
            double p;
            int removed_trees = 0;
            double removed_value = 0.0;
            int no_tree_found = 0;
            bool target_value_reached = false;
            do
            {
                // look up a random number: it decides in which class to select a tree:
                p = RandomGenerator.nrandom(0, 100);
                for (cls = 0; cls < values.Count; ++cls)
                {
                    if (p < custom.classPercentiles[cls + 1])
                    {
                        break;
                    }
                }
                // select a tree:
                int tree_idx = selectRandomTree(trees, percentiles[cls], percentiles[cls + 1] - 1, species_selective);
                if (tree_idx >= 0)
                {
                    // stop harvesting, when the target size is reached: if the current tree would surpass the limit,
                    // a random number decides whether the tree should be included or not.
                    double tree_value = target_dbh ? 1.0 : trees.trees()[tree_idx].Item2;
                    if (custom.targetValue > 0.0)
                    {
                        if (removed_value + tree_value > target_value)
                        {
                            if (RandomGenerator.drandom() > 0.5 || target_value_reached)
                            {
                                break;
                            }
                            else
                            {
                                target_value_reached = true;
                            }
                        }

                    }
                    trees.remove_single_tree(tree_idx, true);
                    removed_trees++;
                    removed_value += tree_value;
                    values[cls]++;
                }
                else
                {
                    // tree_idx = -1: no tree found in list, -2: tree found but is not selected
                    no_tree_found += tree_idx == -1 ? 100 : 1; // empty list counts much more
                    if (no_tree_found > 1000)
                    {
                        finished = true;
                    }
                }
                // stop harvesting, when the minimum remaining number of stems is reached
                if (trees.trees().Count - removed_trees <= custom.remainingStems * stand.area())
                {
                    finished = true;
                }
                if (custom.targetValue > 0.0 && removed_value > target_value)
                {
                    finished = true;
                }
            }
            while (!finished);

            if (stand.trace())
            {
                Debug.WriteLine(stand.context() + " custom-thinning: removed " + removed_trees + ". Reached cumulative 'value' of: " + removed_value + " (planned value: " + target_value + "). #of no trees found: " + no_tree_found + "; stand-area:" + stand.area());
                for (int i = 0; i < values.Count; ++i)
                {
                    Debug.WriteLine(stand.context() + " class " + i + ": removed " + values[i] + " of " + (percentiles[i + 1] - percentiles[i]));
                }
            }

            return true;
        }

        private int selectRandomTree(FMTreeList list, int pct_min, int pct_max, bool selective)
        {
            // pct_min, pct_max: the indices of the first and last tree in the list to be looked for, including pct_max
            // seek a tree in the class 'cls' (which has not already been removed);
            int idx = -1;
            if (pct_max < pct_min)
            {
                return -1;
            }
            // search randomly for a couple of times
            for (int i = 0; i < 5; i++)
            {
                idx = RandomGenerator.irandom(pct_min, pct_max);
                Tree tree = list.trees()[idx].Item1;
                if (!tree.isDead() && !tree.isMarkedForHarvest() && !tree.isMarkedForCut())
                {
                    return selectSelectiveSpecies(list, selective, idx);
                }
            }
            // not found, now walk in a random direction...
            int direction = 1;
            if (RandomGenerator.drandom() > 0.5)
            {
                direction = -1;
            }
            // start in one direction from the last selected random position
            int ridx = idx;
            while (ridx >= pct_min && ridx < pct_max)
            {
                Tree tree = list.trees()[ridx].Item1;
                if (!tree.isDead() && !tree.isMarkedForHarvest() && !tree.isMarkedForCut())
                {
                    return selectSelectiveSpecies(list, selective, ridx);
                }
                ridx += direction;
            }
            // now look in the other direction
            direction = -direction;
            ridx = idx;
            while (ridx >= pct_min && ridx < pct_max)
            {
                Tree tree = list.trees()[ridx].Item1;
                if (!tree.isDead() && !tree.isMarkedForHarvest() && !tree.isMarkedForCut())
                {
                    return selectSelectiveSpecies(list, selective, ridx);
                }
                ridx += direction;
            }

            // no tree found in the entire range
            return -1;
        }

        private int selectSelectiveSpecies(FMTreeList list, bool is_selective, int index)
        {
            if (!is_selective)
            {
                return index;
            }
            // check probability for species [0..1, 0: 0% chance to take a tree of that species] against a random number
            if (mSpeciesSelectivity[list.trees()[index].Item1.species()] < RandomGenerator.drandom())
            {
                return index; // take the tree
            }
            // a tree was found but is not going to be removed
            return -2;
        }

        public void clearTreeMarks(FMTreeList list)
        {
            foreach (MutableTuple<Tree, double> it in list.trees())
            {
                Tree tree = it.Item1;
                if (tree.isMarkedForHarvest())
                {
                    tree.markForHarvest(false);
                }
                if (tree.isMarkedForCut())
                {
                    tree.markForCut(false);
                }
            }
        }

        private bool evaluateSelective(FMStand stand)
        {
            markCropTrees(stand);
            return true;
        }

        private bool markCropTrees(FMStand stand)
        {
            // tree list from current exeution context
            FMTreeList treelist = ForestManagementEngine.instance().scriptBridge().treesObj();
            treelist.setStand(stand);
            treelist.loadAll();
            clearTreeMarks(treelist);

            // get the 2x2m grid for the current stand
            Grid<float> grid = treelist.localGrid();
            // clear (except the out of "stand" pixels)
            for (int p = 0; p < grid.count(); ++p)
            {
                if (grid[p] > -1.0F)
                {
                    grid[p] = 0.0F;
                }
            }

            int target_n = (int)(mSelectiveThinning.N * stand.area());

            if (target_n >= treelist.trees().Count)
            {
                target_n = treelist.trees().Count;
            }

            int max_target_n = (int)Math.Max(target_n * 1.5, treelist.trees().Count / 2.0);
            if (max_target_n >= treelist.trees().Count)
            {
                max_target_n = treelist.trees().Count;
            }
            // we have 2500 px per ha (2m resolution)
            // if each tree dominates its Moore-neighborhood, 2500/9 = 267 trees are possible (/ha)
            // if *more* trees should be marked, some trees need to be on neighbor pixels:
            // pixels = 2500 / N; if 9 px are the Moore neighborhood, the "overprint" is N*9 / 2500.
            // N*)/2500 -1 = probability of having more than zero overlapping pixels
            double overprint = (mSelectiveThinning.N * 9) / (double)(Constant.cPxPerHectare) - 1.0;

            // order the list of trees according to tree height
            treelist.sort("-height");

            // start with a part of N and 0 overlap
            int n_found = 0;
            int tests = 0;
            for (int i = 0; i < target_n / 3; i++)
            {
                float f = testPixel(treelist.trees()[i].Item1.position(), grid); ++tests;
                if (f == 0.0F)
                {
                    setPixel(treelist.trees()[i].Item1.position(), grid);
                    treelist.trees()[i].Item1.markCropTree(true);
                    ++n_found;
                }
            }
            // continue with a higher probability --- incr
            for (int run = 0; run < 4; ++run)
            {
                for (int i = 0; i < max_target_n; ++i)
                {
                    if (treelist.trees()[i].Item1.isMarkedAsCropTree())
                    {
                        continue;
                    }

                    float f = testPixel(treelist.trees()[i].Item1.position(), grid); ++tests;
                    if ((f == 0.0F) ||
                         (f <= 2.0F && RandomGenerator.drandom() < overprint) ||
                         (run == 1 && f <= 4 && RandomGenerator.drandom() < overprint) ||
                         (run == 2 && RandomGenerator.drandom() < overprint) ||
                         (run == 3))
                    {
                        setPixel(treelist.trees()[i].Item1.position(), grid);
                        treelist.trees()[i].Item1.markCropTree(true);
                        ++n_found;
                        if (n_found == target_n)
                        {
                            break;
                        }
                    }
                }
                if (n_found == target_n)
                {
                    break;
                }
            }

            // now mark the competitors:
            // competitors are trees up to 75th percentile of the tree population that
            int n_competitor = 0;
            for (int run = 0; run < 3; ++run)
            {
                for (int i = 0; i < max_target_n; ++i)
                {
                    Tree tree = treelist.trees()[i].Item1;
                    if (tree.isMarkedAsCropTree() || tree.isMarkedAsCropCompetitor())
                    {
                        continue;
                    }

                    float f = testPixel(treelist.trees()[i].Item1.position(), grid); ++tests;

                    if ((f > 12.0F) || (run == 1 && f > 8) || (run == 2 && f > 4))
                    {
                        tree.markCropCompetitor(true);
                        n_competitor++;
                    }
                }
            }

            if (FMSTP.verbose())
            {
                Debug.WriteLine(stand.context() + " Thinning::markCropTrees: marked " + n_found + " (plan: " + target_n + ") from total " + treelist.trees().Count
                             + ". Tests performed: " + tests + " marked as competitors: " + n_competitor);
            }
            return n_found == target_n;

        }

        private float testPixel(PointF pos, Grid<float> grid)
        {
            // check Moore neighborhood
            int x = grid.indexAt(pos).X;
            int y = grid.indexAt(pos).Y;

            float sum = 0.0F;
            sum += grid.isIndexValid(x - 1, y - 1) ? grid.valueAtIndex(x - 1, y - 1) : 0;
            sum += grid.isIndexValid(x, y - 1) ? grid.valueAtIndex(x, y - 1) : 0;
            sum += grid.isIndexValid(x + 1, y - 1) ? grid.valueAtIndex(x + 1, y - 1) : 0;

            sum += grid.isIndexValid(x - 1, y) ? grid.valueAtIndex(x - 1, y) : 0;
            sum += grid.isIndexValid(x, y) ? grid.valueAtIndex(x, y) : 0;
            sum += grid.isIndexValid(x + 1, y) ? grid.valueAtIndex(x + 1, y) : 0;

            sum += grid.isIndexValid(x - 1, y + 1) ? grid.valueAtIndex(x - 1, y + 1) : 0;
            sum += grid.isIndexValid(x, y + 1) ? grid.valueAtIndex(x, y + 1) : 0;
            sum += grid.isIndexValid(x + 1, y + 1) ? grid.valueAtIndex(x + 1, y + 1) : 0;

            return sum;
        }

        private void setPixel(PointF pos, Grid<float> grid)
        {
            // check Moore neighborhood
            int x = grid.indexAt(pos).X;
            int y = grid.indexAt(pos).Y;

            if (grid.isIndexValid(x - 1, y - 1)) grid[x - 1, y - 1]++;
            if (grid.isIndexValid(x, y - 1)) grid[x, y - 1]++;
            if (grid.isIndexValid(x + 1, y - 1)) grid[x + 1, y - 1]++;

            if (grid.isIndexValid(x - 1, y)) grid[x - 1, y]++;
            if (grid.isIndexValid(x, y)) grid[x, y] += 3; // more impact on center pixel
            if (grid.isIndexValid(x + 1, y)) grid[x + 1, y]++;

            if (grid.isIndexValid(x - 1, y + 1)) grid[x - 1, y + 1]++;
            if (grid.isIndexValid(x, y + 1)) grid[x, y + 1]++;
            if (grid.isIndexValid(x + 1, y + 1)) grid[x + 1, y + 1]++;
        }
    }
}
