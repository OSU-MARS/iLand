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
        private static readonly List<string> mSyntaxCustom;

        private class SCustomThinning
        {
            public List<int> classPercentiles; ///< percentiles [0..100] for the classes (count = count(classValues) + 1
            public List<double> classValues; ///< class values (the number of values defines the number of classes)

            public string filter; ///< additional filter
            public bool usePercentiles; ///< if true, classes relate to percentiles, if 'false' classes relate to relative dbh classes
            public bool removal; ///< if true, classes define removals, if false trees should *stay* in the class
            public bool relative; ///< if true, values are per cents, if false, values are absolute values per hectare
            public double targetValue; ///< the number (per ha) that should be removed, see targetVariable
            public bool targetRelative; ///< if true, the target variable is relative to the stock, if false it is absolute
            public string targetVariable; ///< target variable ('volume', 'basalArea', 'stems') / ha
            public double minDbh; ///< only trees with dbh > minDbh are considered (default: 0)
            public int remainingStems; ///< minimum remaining stems/ha (>minDbh)

            public SCustomThinning()
            {
                this.classPercentiles = new List<int>();
                this.classValues = new List<double>();
            }
        };

        private struct SSelectiveThinning
        {
            public int N; // stems per ha target
        };

        private readonly List<SCustomThinning> mCustomThinnings;
        private SSelectiveThinning mSelectiveThinning;
        private readonly Dictionary<Species, double> mSpeciesSelectivity;
        private ThinningType mThinningType;

        static ActThinning()
        {
            ActThinning.mSyntaxCustom = new List<string>(Activity.mAllowedProperties) 
            {
                "percentile", "removal", "thinning", "relative", "remainingStems", "minDbh",
                "filter", "targetVariable", "targetRelative", "targetValue", "classes", "onEvaluate" 
            };
        }

        public ActThinning()
        {
            this.mCustomThinnings = new List<SCustomThinning>();
            this.mSelectiveThinning = new SSelectiveThinning();
            this.mSpeciesSelectivity = new Dictionary<Species, double>();
            this.mThinningType = ThinningType.Invalid;

            this.mBaseActivity.SetIsScheduled(true); // use the scheduler
            this.mBaseActivity.SetDoSimulate(true); // simulate per default
        }

        public override string Type()
        {
            string th = mThinningType switch
            {
                ThinningType.Invalid => "Invalid",
                ThinningType.FromBelow => "from below",
                ThinningType.FromAbove => "from above",
                ThinningType.Custom => "custom",
                ThinningType.Selection => "selection",
                _ => throw new NotSupportedException(),
            };
            return String.Format("thinning ({0})", th);
        }

        public override void Setup(QJSValue value)
        {
            base.Setup(value); // setup base events
            mThinningType = ThinningType.Invalid;
            string th_type = FMSTP.ValueFromJS(value, "thinning").ToString();
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
                case ThinningType.Custom: SetupCustom(value); break;
                case ThinningType.Selection: SetupSelective(value); break;
                default: throw new NotSupportedException("No setup defined for thinning type");
            }
        }

        public override bool Evaluate(FMStand stand)
        {
            bool return_value = true;
            switch (mThinningType)
            {
                case ThinningType.Custom:
                    for (int i = 0; i < mCustomThinnings.Count; ++i)
                    {
                        return_value = return_value && EvaluateCustom(stand, mCustomThinnings[i]);
                    }
                    return return_value; // false if one fails
                case ThinningType.Selection:
                    return EvaluateSelective(stand);
                default:
                    throw new NotSupportedException("evaluate: not available for thinning type");
            }
        }

        public override bool Execute(FMStand stand)
        {
            if (stand.TracingEnabled()) Debug.WriteLine(stand.context() + " execute activity " + name() + ": " + Type());
            if (events().HasEvent("onExecute"))
            {
                // switch off simulation mode
                stand.currentFlags().SetDoSimulate(false);
                // execute this event
                bool result = base.Execute(stand);
                stand.currentFlags().SetDoSimulate(true);
                return result;
            }
            else
            {
                // default behavior: process all marked trees (harvest / cut)
                if (stand.TracingEnabled())
                {
                    Debug.WriteLine(stand.context() + " activity " + name() + " remove all marked trees.");
                }
                FMTreeList trees = new FMTreeList(stand);
                trees.RemoveMarkedTrees();
                return true;
            }
        }

        private void SetupCustom(QJSValue value)
        {
            events().Setup(value, new List<string>() { "onEvaluate" });
            mCustomThinnings.Clear();
            if (value.HasProperty("thinnings") && value.Property("thinnings").IsArray())
            {
                QJSValueIterator it = new QJSValueIterator(value.Property("thinnings"));
                while (it.HasNext())
                {
                    it.Next();
                    if (it.Name() == "length")
                    {
                        continue;
                    }
                    SCustomThinning thinning = new SCustomThinning();
                    mCustomThinnings.Add(thinning);
                    SetupSingleCustom(it.Value(), thinning);
                }
            }
            else
            {
                SCustomThinning thinning = new SCustomThinning();
                mCustomThinnings.Add(thinning);
                SetupSingleCustom(value, thinning);
            }
        }

        private void SetupSelective(QJSValue value)
        {
            mSelectiveThinning.N = FMSTP.ValueFromJS(value, "N", "400").ToInt();
        }

        // setup of the "custom" thinning operation
        private void SetupSingleCustom(QJSValue value, SCustomThinning custom)
        {
            FMSTP.CheckObjectProperties(value, mSyntaxCustom, "setup of 'custom' thinning:" + name());

            custom.usePercentiles = FMSTP.BoolValueFromJS(value, "percentile", true);
            custom.removal = FMSTP.BoolValueFromJS(value, "removal", true);
            custom.relative = FMSTP.BoolValueFromJS(value, "relative", true);
            custom.remainingStems = FMSTP.ValueFromJS(value, "remainingStems", "0").ToInt();
            custom.minDbh = FMSTP.ValueFromJS(value, "minDbh", "0").ToNumber();
            QJSValue filter = FMSTP.ValueFromJS(value, "filter", "");
            if (filter.IsString())
            {
                custom.filter = filter.ToString();
            }
            else
            {
                custom.filter = null;
            }
            custom.targetVariable = FMSTP.ValueFromJS(value, "targetVariable", "stems").ToString();
            if (custom.targetVariable != "stems" && custom.targetVariable != "basalArea" && custom.targetVariable != "volume")
            {
                throw new NotSupportedException(String.Format("setup of custom Activity: invalid targetVariable: {0}", custom.targetVariable));
            }
            custom.targetRelative = FMSTP.BoolValueFromJS(value, "targetRelative", true);
            custom.targetValue = FMSTP.ValueFromJS(value, "targetValue", "30").ToNumber();
            if (custom.targetRelative && (custom.targetValue > 100.0 || custom.targetValue < 0.0))
            {
                throw new NotSupportedException(String.Format("setup of custom Activity: invalid relative targetValue (0-100): {0}", custom.targetValue));
            }

            QJSValue values = FMSTP.ValueFromJS(value, "classes", "", "setup custom acitvity");
            if (!values.IsArray())
            {
                throw new NotSupportedException("setup of custom activity: the 'classes' is not an array.");
            }
            custom.classValues.Clear();
            custom.classPercentiles.Clear();
            QJSValueIterator it = new QJSValueIterator(values);
            while (it.HasNext())
            {
                it.Next();
                if (it.Name() == "length")
                {
                    continue;
                }
                custom.classValues.Add(it.Value().ToNumber());
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

        private bool EvaluateCustom(FMStand stand, SCustomThinning custom)
        {
            // fire onEvaluate event and collect probabilities
            QJSValue eval_result = events().Run("onEvaluate", stand);
            if (eval_result.IsBool() && eval_result.ToBool() == false)
            {
                return false; // do nothing
            }
            bool species_selective = false;

            if (eval_result.IsObject())
            {
                // expecting a list of probabilities....
                // create list if not present
                if (mSpeciesSelectivity.Count == 0)
                {
                    foreach (Species s in GlobalSettings.Instance.Model.SpeciesSet().ActiveSpecies)
                    {
                        mSpeciesSelectivity[s] = 1.0;
                    }
                }
                // fetch from javascript
                double rest_val = eval_result.Property("rest").IsNumber() ? eval_result.Property("rest").ToNumber() : 1.0;
                foreach (Species s in mSpeciesSelectivity.Keys)
                {
                    mSpeciesSelectivity[s] = Global.Limit(eval_result.Property(s.ID).IsNumber() ? eval_result.Property(s.ID).ToNumber() : rest_val, 0.0, 1.0);
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
                trees.Load(filter);
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
            ClearTreeMarks(trees);

            // sort always by target variable (if it is stems, then simply by dbh)
            bool target_dbh = custom.targetVariable == "stems";
            if (target_dbh)
            {
                trees.Sort("dbh");
            }
            else
            {
                trees.Sort(custom.targetVariable);
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

            double target_value;
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
                p = RandomGenerator.Random(0, (double)100);
                for (cls = 0; cls < values.Count; ++cls)
                {
                    if (p < custom.classPercentiles[cls + 1])
                    {
                        break;
                    }
                }
                // select a tree:
                int tree_idx = SelectRandomTree(trees, percentiles[cls], percentiles[cls + 1] - 1, species_selective);
                if (tree_idx >= 0)
                {
                    // stop harvesting, when the target size is reached: if the current tree would surpass the limit,
                    // a random number decides whether the tree should be included or not.
                    double tree_value = target_dbh ? 1.0 : trees.trees()[tree_idx].Item2;
                    if (custom.targetValue > 0.0)
                    {
                        if (removed_value + tree_value > target_value)
                        {
                            if (RandomGenerator.Random() > 0.5 || target_value_reached)
                            {
                                break;
                            }
                            else
                            {
                                target_value_reached = true;
                            }
                        }

                    }
                    trees.RemoveSingleTree(tree_idx, true);
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

            if (stand.TracingEnabled())
            {
                Debug.WriteLine(stand.context() + " custom-thinning: removed " + removed_trees + ". Reached cumulative 'value' of: " + removed_value + " (planned value: " + target_value + "). #of no trees found: " + no_tree_found + "; stand-area:" + stand.area());
                for (int i = 0; i < values.Count; ++i)
                {
                    Debug.WriteLine(stand.context() + " class " + i + ": removed " + values[i] + " of " + (percentiles[i + 1] - percentiles[i]));
                }
            }

            return true;
        }

        private int SelectRandomTree(FMTreeList list, int pct_min, int pct_max, bool selective)
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
                idx = RandomGenerator.Random(pct_min, pct_max);
                Tree tree = list.trees()[idx].Item1;
                if (!tree.IsDead() && !tree.IsMarkedForHarvest() && !tree.IsMarkedForCut())
                {
                    return SelectSelectiveSpecies(list, selective, idx);
                }
            }
            // not found, now walk in a random direction...
            int direction = 1;
            if (RandomGenerator.Random() > 0.5)
            {
                direction = -1;
            }
            // start in one direction from the last selected random position
            int ridx = idx;
            while (ridx >= pct_min && ridx < pct_max)
            {
                Tree tree = list.trees()[ridx].Item1;
                if (!tree.IsDead() && !tree.IsMarkedForHarvest() && !tree.IsMarkedForCut())
                {
                    return SelectSelectiveSpecies(list, selective, ridx);
                }
                ridx += direction;
            }
            // now look in the other direction
            direction = -direction;
            ridx = idx;
            while (ridx >= pct_min && ridx < pct_max)
            {
                Tree tree = list.trees()[ridx].Item1;
                if (!tree.IsDead() && !tree.IsMarkedForHarvest() && !tree.IsMarkedForCut())
                {
                    return SelectSelectiveSpecies(list, selective, ridx);
                }
                ridx += direction;
            }

            // no tree found in the entire range
            return -1;
        }

        private int SelectSelectiveSpecies(FMTreeList list, bool is_selective, int index)
        {
            if (!is_selective)
            {
                return index;
            }
            // check probability for species [0..1, 0: 0% chance to take a tree of that species] against a random number
            if (mSpeciesSelectivity[list.trees()[index].Item1.Species] < RandomGenerator.Random())
            {
                return index; // take the tree
            }
            // a tree was found but is not going to be removed
            return -2;
        }

        public void ClearTreeMarks(FMTreeList list)
        {
            foreach (MutableTuple<Tree, double> it in list.trees())
            {
                Tree tree = it.Item1;
                if (tree.IsMarkedForHarvest())
                {
                    tree.MarkForHarvest(false);
                }
                if (tree.IsMarkedForCut())
                {
                    tree.MarkForCut(false);
                }
            }
        }

        private bool EvaluateSelective(FMStand stand)
        {
            MarkCropTrees(stand);
            return true;
        }

        private bool MarkCropTrees(FMStand stand)
        {
            // tree list from current exeution context
            FMTreeList treelist = ForestManagementEngine.instance().scriptBridge().treesObj();
            treelist.SetStand(stand);
            treelist.loadAll();
            ClearTreeMarks(treelist);

            // get the 2x2m grid for the current stand
            Grid<float> grid = treelist.localGrid();
            // clear (except the out of "stand" pixels)
            for (int p = 0; p < grid.Count; ++p)
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
            double overprint = (mSelectiveThinning.N * 9) / (double)(Constant.LightCellsPerHectare) - 1.0;

            // order the list of trees according to tree height
            treelist.Sort("-height");

            // start with a part of N and 0 overlap
            int n_found = 0;
            int tests = 0;
            for (int i = 0; i < target_n / 3; i++)
            {
                float f = TestPixel(treelist.trees()[i].Item1.GetCellCenterPoint(), grid); ++tests;
                if (f == 0.0F)
                {
                    SetPixel(treelist.trees()[i].Item1.GetCellCenterPoint(), grid);
                    treelist.trees()[i].Item1.MarkAsCropTree(true);
                    ++n_found;
                }
            }
            // continue with a higher probability --- incr
            for (int run = 0; run < 4; ++run)
            {
                for (int i = 0; i < max_target_n; ++i)
                {
                    if (treelist.trees()[i].Item1.IsMarkedAsCropTree())
                    {
                        continue;
                    }

                    float f = TestPixel(treelist.trees()[i].Item1.GetCellCenterPoint(), grid); ++tests;
                    if ((f == 0.0F) ||
                         (f <= 2.0F && RandomGenerator.Random() < overprint) ||
                         (run == 1 && f <= 4 && RandomGenerator.Random() < overprint) ||
                         (run == 2 && RandomGenerator.Random() < overprint) ||
                         (run == 3))
                    {
                        SetPixel(treelist.trees()[i].Item1.GetCellCenterPoint(), grid);
                        treelist.trees()[i].Item1.MarkAsCropTree(true);
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
                    if (tree.IsMarkedAsCropTree() || tree.IsMarkedAsCropCompetitor())
                    {
                        continue;
                    }

                    float f = TestPixel(treelist.trees()[i].Item1.GetCellCenterPoint(), grid); ++tests;

                    if ((f > 12.0F) || (run == 1 && f > 8) || (run == 2 && f > 4))
                    {
                        tree.MarkAsCropCompetitor(true);
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

        private float TestPixel(PointF pos, Grid<float> grid)
        {
            // check Moore neighborhood
            int x = grid.IndexAt(pos).X;
            int y = grid.IndexAt(pos).Y;

            float sum = 0.0F;
            sum += grid.Contains(x - 1, y - 1) ? grid[x - 1, y - 1] : 0;
            sum += grid.Contains(x, y - 1) ? grid[x, y - 1] : 0;
            sum += grid.Contains(x + 1, y - 1) ? grid[x + 1, y - 1] : 0;

            sum += grid.Contains(x - 1, y) ? grid[x - 1, y] : 0;
            sum += grid.Contains(x, y) ? grid[x, y] : 0;
            sum += grid.Contains(x + 1, y) ? grid[x + 1, y] : 0;

            sum += grid.Contains(x - 1, y + 1) ? grid[x - 1, y + 1] : 0;
            sum += grid.Contains(x, y + 1) ? grid[x, y + 1] : 0;
            sum += grid.Contains(x + 1, y + 1) ? grid[x + 1, y + 1] : 0;

            return sum;
        }

        private void SetPixel(PointF pos, Grid<float> grid)
        {
            // check Moore neighborhood
            int x = grid.IndexAt(pos).X;
            int y = grid.IndexAt(pos).Y;

            if (grid.Contains(x - 1, y - 1)) grid[x - 1, y - 1]++;
            if (grid.Contains(x, y - 1)) grid[x, y - 1]++;
            if (grid.Contains(x + 1, y - 1)) grid[x + 1, y - 1]++;

            if (grid.Contains(x - 1, y)) grid[x - 1, y]++;
            if (grid.Contains(x, y)) grid[x, y] += 3; // more impact on center pixel
            if (grid.Contains(x + 1, y)) grid[x + 1, y]++;

            if (grid.Contains(x - 1, y + 1)) grid[x - 1, y + 1]++;
            if (grid.Contains(x, y + 1)) grid[x, y + 1]++;
            if (grid.Contains(x + 1, y + 1)) grid[x + 1, y + 1]++;
        }
    }
}
