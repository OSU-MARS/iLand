using iLand.core;
using iLand.tools;
using System;
using System.Drawing;

namespace iLand.abe
{
    internal class SPlantingItem
    {
        public Species species;
        public double fraction;
        public double height;
        public int age;
        public bool clear;
        public bool grouped; ///< true for pattern creation
        public int group_type; ///< index of the pattern in the pattern list
        public int group_random_count; ///< if >0: number of random patterns
        public int offset; ///< offset (in LIF-pixels) for the pattern algorithm
        public int spacing;  ///< distance between two applications of a pattern

        public SPlantingItem()
        {
            species = null;
            fraction = 0.0;
            height = 0.05;
            age = 1;
            clear = false;
            grouped = false;
            group_type = -1;
            group_random_count = -1;
            offset = 0;
            spacing = 0;
        }

        public bool Setup(QJSValue value)
        {
            string species_id = FMSTP.ValueFromJS(value, "species", null, "setup of planting item for planting activity.").ToString();
            species = GlobalSettings.Instance.Model.SpeciesSet().GetSpecies(species_id);
            if (species == null)
            {
                throw new NotSupportedException(String.Format("'{0}' is not a valid species id for setting up a planting item.", species_id));
            }
            fraction = FMSTP.ValueFromJS(value, "fraction", "0").ToNumber();
            height = FMSTP.ValueFromJS(value, "height", "0.05").ToNumber();
            age = FMSTP.ValueFromJS(value, "age", "1").ToInt();
            clear = FMSTP.ValueFromJS(value, "clear", "false").ToBool();

            // pattern
            string group = FMSTP.ValueFromJS(value, "pattern", "").ToString();
            group_type = ActPlanting.planting_pattern_names.IndexOf(group);
            if (String.IsNullOrEmpty(group) == false && group != "undefined" && group_type == -1)
            {
                throw new NotSupportedException(String.Format("Planting-activity: the pattern '{0}' is not valid!", group));
            }
            spacing = FMSTP.ValueFromJS(value, "spacing", "0").ToInt();
            offset = FMSTP.ValueFromJS(value, "offset", "0").ToInt();

            bool random = FMSTP.BoolValueFromJS(value, "random", false);
            if (random)
            {
                group_random_count = FMSTP.ValueFromJS(value, "n", "0").ToInt();
            }
            else
            {
                group_random_count = 0;
            }
            grouped = group_type >= 0;
            return true;
        }

        public void Run(FMStand stand)
        {
            RectangleF box = ForestManagementEngine.StandGrid().BoundingBox(stand.id());
            MapGrid sgrid = ForestManagementEngine.StandGrid();
            Model model = GlobalSettings.Instance.Model;
            GridRunner<float> runner = new GridRunner<float>(model.LightGrid, box);
            if (!grouped)
            {
                // distribute saplings randomly.
                // this adds saplings to SaplingCell (only if enough slots are available)
                for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
                {
                    if (sgrid.StandIDFromLifCoord(runner.CurrentIndex()) != stand.id())
                    {
                        continue;
                    }
                    if (RandomGenerator.Random() < fraction)
                    {
                        ResourceUnit ru = model.GetResourceUnit(runner.CurrentCoordinate());
                        ru.SaplingCell(runner.CurrentIndex()).AddSapling((float)height, age, species.Index);
                    }
                }
            }
            else
            {
                // grouped saplings
                string pp = ActPlanting.planting_patterns[group_type].Item1;
                int n = ActPlanting.planting_patterns[group_type].Item2;

                if (spacing == 0 && group_random_count == 0)
                {
                    // pattern based planting (filled)
                    runner.Reset();
                    for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
                    {
                        Point qp = runner.CurrentIndex();
                        if (sgrid.StandIDFromLifCoord(qp) != stand.id())
                        {
                            continue;
                        }
                        // check location in the pre-defined planting patterns
                        int idx = (qp.X + offset) % n + n * ((qp.Y + offset) % n);
                        if (pp[idx] == '1')
                        {
                            ResourceUnit ru = model.GetResourceUnit(runner.CurrentCoordinate());
                            SaplingCell sc = ru.SaplingCell(qp);

                            if (clear)
                            {
                                // clear all sapling trees on the cell
                                model.Saplings.ClearSaplings(sc, ru, true);
                            }
                            sc.AddSapling((float)height, age, species.Index);
                        }
                    }
                }
                else
                {
                    // pattern based (with spacing / offset, random...)
                    int ispacing = spacing / Constant.LightSize;
                    Point p = model.LightGrid.IndexAt(box.TopLeft()).Subtract(new Point(offset, offset));
                    Point pstart = p;
                    Point p_end = model.LightGrid.IndexAt(box.BottomRight());
                    Point po;
                    p.X = Math.Max(p.X, 0);
                    p.Y = Math.Max(p.Y, 0);

                    int n_ha = (int)(group_random_count * box.Width * box.Height / 10000.0);
                    bool do_random = group_random_count > 0;

                    while (p.X < p_end.X && p.Y < p_end.Y)
                    {
                        if (do_random)
                        {
                            // random position!
                            if (n_ha-- <= 0)
                                break;
                            // select a random position (2m grid index)
                            p = model.LightGrid.IndexAt(new PointF((float)RandomGenerator.Random(box.Left, box.Right), (float)RandomGenerator.Random(box.Top, box.Bottom)));
                        }

                        // apply the pattern....
                        for (int y = 0; y < n; ++y)
                        {
                            for (int x = 0; x < n; ++x)
                            {
                                po = p.Add(new Point(x, y));
                                if (sgrid.StandIDFromLifCoord(po) != stand.id())
                                {
                                    continue;
                                }
                                ResourceUnit ru = model.GetResourceUnit(model.LightGrid.GetCellCenterPoint(po));
                                SaplingCell sc = ru.SaplingCell(po);

                                if (clear)
                                {
                                    // clear all sapling trees
                                    model.Saplings.ClearSaplings(sc, ru, true);
                                }
                                sc.AddSapling((float)height, age, species.Index);
                            }
                        }
                        if (!do_random)
                        {
                            // apply offset
                            p.X += ispacing;
                            if (p.X >= p_end.X)
                            {
                                p.X = pstart.X;
                                p.Y += ispacing;
                            }
                        }
                    }
                }
            }
        }
    }
}
