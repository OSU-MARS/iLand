using iLand.core;
using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.abe
{
    /** @class ActSalvage
        @ingroup abe
        The ActSalvage class handles salvage logging after disturbances.
        */
    internal class ActSalvage : Activity
    {
        private bool mDebugSplit;
        private Expression mCondition; ///< formula to determine which trees should be harvested
        private int mMaxPreponeActivity; ///< no of years that a already scheduled (regular) activity is 'preponed'
        private double mThresholdMinimal; ///< lower threshold (below no action is taken) in m3/ha
        private double mThresholdSplit; ///<threshold (relative damage, 0..1) when a split of the stand should be initiated
        private double mThresholdClear; ///<threshold (relative damage, 0..1) when a stand should be completely cleared

        public override string Type() { return "salvage"; }

        public ActSalvage()
        {
            mCondition = null;
            mMaxPreponeActivity = 0;

            mBaseActivity.SetIsSalvage(true);
            mBaseActivity.SetIsRepeating(true);
            mBaseActivity.SetIsExecuteImmediate(true);
        }

        public override void Setup(QJSValue value)
        {
            base.Setup(value); // setup base events
            events().Setup(value, new List<string>() { "onBarkBeetleAttack" });

            string condition = FMSTP.ValueFromJS(value, "disturbanceCondition").ToString();
            if (String.IsNullOrEmpty(condition) && condition != "undefined")
            {
                mCondition = new Expression(condition);
            }
            mMaxPreponeActivity = FMSTP.ValueFromJS(value, "maxPrepone", "0").ToInt();
            mThresholdSplit = FMSTP.ValueFromJS(value, "thresholdSplitStand", "0.1").ToNumber();
            mThresholdClear = FMSTP.ValueFromJS(value, "thresholdClearStand", "0.9").ToNumber();
            mThresholdMinimal = FMSTP.ValueFromJS(value, "thresholdIgnoreDamage", "5").ToNumber();
            mDebugSplit = FMSTP.BoolValueFromJS(value, "debugSplit", false);
        }

        public override bool Execute(FMStand stand)
        {
            if (stand.Property("_run_salvage").ToBool())
            {
                // 2nd phase: do the after disturbance cleanup of a stand.
                bool simu = stand.currentFlags().DoSimulate();
                stand.currentFlags().SetDoSimulate(false);
                stand.currentFlags().SetIsFinalHarvest(true); // this should be accounted as "final harvest"
                                                            // execute the "onExecute" event
                bool result = base.Execute(stand);
                stand.currentFlags().SetDoSimulate(simu);
                stand.currentFlags().SetIsFinalHarvest(false);
                stand.SetProperty("_run_salvage", new QJSValue(false));
                return result;
            }

            // the salvaged timber is already accounted for - so nothing needs to be done here.
            // however, we check if there is a planned activity for the stand which could be executed sooner
            // than planned.
            bool preponed = stand.unit().scheduler().ForceHarvest(stand, mMaxPreponeActivity);
            if (stand.TracingEnabled())
            {
                Debug.WriteLine("Salvage activity executed. Changed scheduled activites (preponed): " + preponed);
            }

            stand.unit().scheduler().AddExtraHarvest(stand, stand.TotalHarvest(), HarvestType.Salvage);
            // check if we should re-assess the stand grid (after large disturbances)
            // as a preliminary check we only look closer, if we have more than  x m3/ha of damage.
            if (stand.disturbedTimber() / stand.area() > mThresholdMinimal)
            {
                CheckStandAfterDisturbance(stand);
            }
            // the harvest happen(ed) anyways.
            //stand.resetHarvestCounter(); // set back to zero...
            return true;
        }

        public override List<string> Info()
        {
            List<string> lines = base.Info();
            lines.Add(String.Format("condition: {0}", mCondition != null ? mCondition.ExpressionString : "-"));
            lines.Add(String.Format("maxPrepone: {0}", mMaxPreponeActivity));
            return lines;
        }

        public bool EvaluateRemove(Tree tree)
        {
            if (mCondition == null)
            {
                return true; // default: remove all trees
            }
            TreeWrapper tw = new TreeWrapper(tree);
            bool result = mCondition.Execute(null, tw) != 0.0;
            return result;
        }

        public bool BarkbeetleAttack(FMStand stand, double generations, int infested_px_ha)
        {
            //QJSValue params;
            List < QJSValue > parameters = new List<QJSValue>() { new QJSValue(generations), new QJSValue(infested_px_ha) };

            QJSValue result = events().Run("onBarkBeetleAttack", stand, parameters);
            if (!result.IsBool())
            {
                Debug.WriteLine("Salvage-Activity:onBarkBeetleAttack: expecting a boolean return");
            }
            return result.ToBool();
        }

        private void CheckStandAfterDisturbance(FMStand stand)
        {
            FMTreeList trees = ForestManagementEngine.instance().scriptBridge().treesObj();
            //trees.runGrid();
            trees.PrepareStandGrid("height", null);

            int min_split_size = 50; // min size (100=1ha)
            Grid<float> grid = trees.standGrid();
            int no_split = 0;
            if (mDebugSplit)
            {
                trees.ExportStandGrid(String.Format("temp/height_{0}.txt", ++no_split));
            }

            float h_max = grid.Max();

            double r_low;
            int h_lower = 0, h_higher = 0;
            if (h_max == 0.0F)
            {
                // total disturbance...
                r_low = 1.0;
            }
            else
            {
                // check coverage of disturbed area.
                for (int p = 0; p < grid.Count; ++p)
                {
                    if (grid[p] >= 0.0F)
                    {
                        if (grid[p] < h_max * 0.33)
                        {
                            ++h_lower;
                        }
                        else
                        {
                            ++h_higher;
                        }
                    }
                }
                if (h_lower == 0 && h_higher == 0)
                {
                    return;
                }
                r_low = h_lower / (double)(h_lower + h_higher);
            }

            if (r_low < mThresholdSplit || (r_low < 0.5 && h_lower < min_split_size))
            {
                // no big damage: return and do nothing
                return;
            }

            // restart if a large fraction is cleared, or if the remaining forest is <0.25ha
            if (r_low > mThresholdClear || (r_low > 0.5 && h_higher < min_split_size))
            {
                // total disturbance: restart rotation...
                Debug.WriteLine("ActSalvage: total damage for stand " + stand.id() + " Restarting rotation.");
                stand.SetProperty("_run_salvage", new QJSValue(true));
                stand.Reset(stand.stp());
                return;
            }
            // medium disturbance: check if need to split the stand area:
            Grid<int> my_map = new Grid<int>(grid.CellSize, grid.SizeX, grid.SizeY);
            GridRunner<float> runner = new GridRunner<float>(grid);
            GridRunner<int> id_runner = new GridRunner<int>(my_map);
            float[] neighbors = new float[8];
            int n_empty = 0;
            for (runner.MoveNext(), id_runner.MoveNext(); runner.IsValid() && id_runner.IsValid(); runner.MoveNext(), id_runner.MoveNext())
            {
                if (runner.Current == -1.0)
                {
                    id_runner.Current = -1;
                    continue;
                }
                runner.Neighbors8(neighbors);
                double empty = 0.0;
                int valid = 0;
                for (int i = 0; i < 8; ++i)
                {
                    if (neighbors[i] != 0.0 && neighbors[i] < h_max * 0.33)
                    {
                        empty++;
                    }
                    if (neighbors[i] != 0.0)
                    {
                        valid++;
                    }
                }
                if (valid != 0)
                {
                    empty /= (double)valid;
                }
                // empty cells are marked with 0; areas covered by forest set to stand_id; -1: out-of-stand areas
                // if a cell is empty, some neighbors (i.e. >50%) need to be empty too;
                // if a cell is *not* empty, it has to be surrounded by a larger fraction of empty points (75%)
                if ((runner.Current < h_max * 0.33 && empty > 0.5) || (empty >= 0.75))
                {
                    id_runner.Current = 0;
                    n_empty++;
                }
                else
                {
                    id_runner.Current = stand.id();
                }
            }
            if (mDebugSplit)
            {
                Helper.SaveToTextFile(GlobalSettings.Instance.Path(String.Format("temp/split_before_{0}.txt", no_split)), Grid.ToEsriRaster(my_map));
            }

            // now flood-fill 0ed areas....
            // if the "new" areas are too small (<0.25ha), then nothing happens.
            List<MutableTuple<int, int>> cleared_small_areas = new List<MutableTuple<int, int>>(); // areas of cleared "patches"
            List<MutableTuple<int, int>> stand_areas = new List<MutableTuple<int, int>>(); // areas of remaining forested "patches"
            int fill_color = -1;
            int stand_fill_color = stand.id() + 1000;
            id_runner.Reset();
            while (id_runner.MoveNext() != 0)
            {
                if (id_runner.Current == 0)
                {
                    int s = FloodFillHelper(my_map, id_runner.CurrentIndex(), 0, --fill_color);
                    cleared_small_areas.Add(new MutableTuple<int, int>(fill_color, s)); // patch size
                }
                else if (id_runner.Current == stand.id())
                {
                    int s = FloodFillHelper(my_map, id_runner.CurrentIndex(), stand.id(), stand_fill_color);
                    stand_areas.Add(new MutableTuple<int, int>(stand_fill_color, s));
                    stand_fill_color++;
                }
            }
            if (mDebugSplit)
            {
                Helper.SaveToTextFile(GlobalSettings.Instance.Path(String.Format("temp/split_stands_{0}.txt", no_split)), Grid.ToEsriRaster(my_map));
            }

            // special case: remainnig forest are only small patches
            int max_size = 0;
            for (int i = 0; i < stand_areas.Count; ++i)
            {
                max_size = Math.Max(max_size, stand_areas[i].Item2);
            }
            if (max_size < min_split_size)
            {
                // total disturbance: restart rotation...
                Debug.WriteLine("ActSalvage: total damage for stand " + stand.id() + " (remaining patches too small). Restarting rotation.");
                stand.SetProperty("_run_salvage", new QJSValue(true));
                stand.Reset(stand.stp());
                return;
            }

            // clear small areas
            List<int> neighbor_ids = new List<int>();
            bool finished = false;
            int iter = 100;
            while (!finished && cleared_small_areas.Count > 0 && --iter > 0)
            {
                // find smallest area....
                int i_min = -1;
                for (int i = 0; i < cleared_small_areas.Count; ++i)
                {
                    if (cleared_small_areas[i].Item2 < min_split_size)
                    {
                        if (i_min == -1 || (i_min > -1 && cleared_small_areas[i].Item2 < cleared_small_areas[i_min].Item2))
                            i_min = i;
                    }
                }
                if (i_min == -1)
                {
                    finished = true;
                    continue;
                }

                // loook for neighbors of the area
                // attach to largest "cleared" neighbor (if such a neighbor exists)
                NeighborFinderHelper(my_map, neighbor_ids, cleared_small_areas[i_min].Item1);
                if (neighbor_ids.Count == 0)
                {
                    // patch fully surrounded by "out of project area". We'll add it to the *first* stand map entry
                    neighbor_ids.Add(stand_areas[0].Item1);
                }
                // look for "empty patches" first
                int i_empty = -1; 
                max_size = 0;
                for (int i = 0; i < cleared_small_areas.Count; ++i)
                {
                    if (neighbor_ids.Contains(cleared_small_areas[i].Item1))
                    {
                        if (cleared_small_areas[i].Item2 > max_size)
                        {
                            i_empty = i;
                            max_size = cleared_small_areas[i].Item2;
                        }
                    }
                }
                if (i_empty > -1)
                {
                    // replace "i_min" with "i_empty"
                    int r = ReplaceValueHelper(my_map, cleared_small_areas[i_min].Item1, cleared_small_areas[i_empty].Item1);
                    cleared_small_areas[i_empty].Item2 += r;
                    cleared_small_areas.RemoveAt(i_min);
                    continue;
                }

                if (stand_areas.Count > 0)
                {
                    // attach to largest stand part which is a neighbor
                    i_empty = -1; 
                    max_size = 0;
                    for (int i = 0; i < stand_areas.Count; ++i)
                    {
                        if (neighbor_ids.Contains(stand_areas[i].Item1))
                        {
                            if (stand_areas[i].Item2 > max_size)
                            {
                                i_empty = i;
                                max_size = stand_areas[i].Item2;
                            }
                        }
                    }
                    if (i_empty > -1)
                    {
                        // replace "i_min" with "i_empty"
                        int r = ReplaceValueHelper(my_map, cleared_small_areas[i_min].Item1, stand_areas[i_empty].Item1);
                        stand_areas[i_empty].Item2 += r;
                        cleared_small_areas.RemoveAt(i_min);
                    }
                }
                if (iter == 3)
                {
                    Debug.WriteLine("ActSalvage:Loop1: no solution.");
                }
            }

            // clear small forested stands
            finished = false;
            iter = 100;
            while (!finished && --iter > 0)
            {
                finished = true;
                for (int i = 0; i < stand_areas.Count; ++i)
                {
                    if (stand_areas[i].Item2 < min_split_size)
                    {
                        NeighborFinderHelper(my_map, neighbor_ids, stand_areas[i].Item1);

                        if (neighbor_ids.Count > 0)
                        {
                            int r = ReplaceValueHelper(my_map, stand_areas[i].Item1, neighbor_ids[0]);
                            if (neighbor_ids[0] > 0)
                            {
                                // another stand
                                for (int j = 0; j < stand_areas.Count; ++j)
                                {
                                    if (stand_areas[j].Item1 == neighbor_ids[0])
                                    {
                                        stand_areas[j].Item2 += r;
                                    }
                                }
                            }
                            else
                            {
                                // clearing
                                for (int j = 0; j < cleared_small_areas.Count; ++j)
                                {
                                    if (cleared_small_areas[j].Item1 == neighbor_ids[0])
                                    {
                                        cleared_small_areas[j].Item2 += r;
                                    }
                                }
                            }

                            stand_areas.RemoveAt(i);
                            finished = false;
                            break;
                        }
                    }
                }
            }
            if (iter == 0)
            {
                Debug.WriteLine("ActSalvage:Loop2: no solution.");
            }
            if (mDebugSplit)
            {
                Helper.SaveToTextFile(GlobalSettings.Instance.Path(String.Format("temp/split_final_{0}.txt", no_split)), Grid.ToEsriRaster(my_map));
            }

            // determine final new stands....
            List<int> new_stands = new List<int>(); // internal ids that should become new stands
            for (int i = 0; i < cleared_small_areas.Count; ++i)
            {
                new_stands.Add(cleared_small_areas[i].Item1);
            }

            // only add new stands - keep the old stand as is
            //    if (new_stands.Count>0) {
            //        // if there are no "cleared" parts, we keep the stand as is.
            //        for (int i=0;i<stand_areas.Count;++i)
            //            if (stand_areas[i].Item1 != stand.id()+1000)
            //                new_stands.Add(stand_areas[i].Item1);
            //    }

            for (int i = 0; i < new_stands.Count; ++i)
            {
                // ok: we have new stands. Now do the actual splitting
                FMStand new_stand = ForestManagementEngine.instance().SplitExistingStand(stand);
                // copy back to the stand grid
                GridRunner<int> sgrid = new GridRunner<int>(ForestManagementEngine.StandGrid().Grid, grid.PhysicalSize);
                id_runner.Reset();
                int n_px = 0;
                for (sgrid.MoveNext(), id_runner.MoveNext(); sgrid.IsValid() && id_runner.IsValid(); sgrid.MoveNext(), id_runner.MoveNext())
                {
                    if (id_runner.Current == new_stands[i])
                    {
                        sgrid.Current = new_stand.id();
                        ++n_px;
                    }
                }

                // the new stand  is prepared.
                // at the end of this years execution, the stand will be re-evaluated.
                new_stand.setInitialId(stand.id());
                // year of splitting: all the area of the stand is still accounted for for the "old" stand
                // in the next year (after the update of the stand grid), the old stand shrinks and the new
                // stands get their correct size.
                // new_stand.setArea(n_px / (cHeightSize*cHeightSize));
                new_stand.SetProperty("_run_salvage", new QJSValue(true));
                new_stand.Reset(stand.stp());
                Debug.WriteLine("ActSalvage: new stand " + new_stand.id() + " parent stand " + stand.id() + " #split: " + no_split);
            }
        }

        // quick and dirty implementation of the flood fill algroithm.
        // based on: http://en.wikipedia.org/wiki/Flood_fill
        // returns the number of pixels colored
        private int FloodFillHelper(Grid<int> grid, Point start, int old_color, int color)
        {
            Queue<Point> pqueue = new Queue<Point>();
            pqueue.Enqueue(start);
            int found = 0;
            while (pqueue.Count != 0)
            {
                Point p = pqueue.Dequeue();
                if (!grid.Contains(p))
                {
                    continue;
                }
                if (grid[p] == old_color)
                {
                    grid[p] = color;
                    pqueue.Enqueue(p.Add(new Point(-1, 0)));
                    pqueue.Enqueue(p.Add(new Point(1, 0)));
                    pqueue.Enqueue(p.Add(new Point(0, -1)));
                    pqueue.Enqueue(p.Add(new Point(0, 1)));
                    pqueue.Enqueue(p.Add(new Point(1, 1)));
                    pqueue.Enqueue(p.Add(new Point(1, -1)));
                    pqueue.Enqueue(p.Add(new Point(-1, 1)));
                    pqueue.Enqueue(p.Add(new Point(-1, -1)));
                    ++found;
                }
            }
            return found;
        }

        // find all neigbors of color 'stand_id' and save in the 'neighbors' vector
        private int NeighborFinderHelper(Grid<int> grid, List<int> neighbors, int stand_id)
        {
            GridRunner<int> id_runner = new GridRunner<int>(grid);
            neighbors.Clear();
            int[] nb = new int[8];
            for (id_runner.MoveNext(); id_runner.IsValid(); id_runner.MoveNext())
            {
                if (id_runner.Current == stand_id)
                {
                    id_runner.Neighbors8(nb);
                    for (int i = 0; i < 8; ++i)
                    {
                        if (nb[i] != 0 && nb[i] != -1 && nb[i] != stand_id)
                        {
                            if (!neighbors.Contains(nb[i]))
                            {
                                neighbors.Add(nb[i]);
                            }
                        }
                    }
                }
            }
            return neighbors.Count;
        }

        private int ReplaceValueHelper(Grid<int> grid, int old_value, int new_value)
        {
            int n = 0;
            for (int p = 0; p < grid.Count; ++p)
            {
                if (grid[p] == old_value)
                {
                    grid[p] = new_value;
                    ++n;
                }
            }
            return n;
        }
    }
}
