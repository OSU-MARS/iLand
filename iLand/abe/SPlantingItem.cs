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

        public bool setup(QJSValue value)
        {
            string species_id = FMSTP.valueFromJs(value, "species", null, "setup of planting item for planting activity.").toString();
            species = GlobalSettings.instance().model().speciesSet().species(species_id);
            if (species == null)
            {
                throw new NotSupportedException(String.Format("'{0}' is not a valid species id for setting up a planting item.", species_id));
            }
            fraction = FMSTP.valueFromJs(value, "fraction", "0").toNumber();
            height = FMSTP.valueFromJs(value, "height", "0.05").toNumber();
            age = FMSTP.valueFromJs(value, "age", "1").toInt();
            clear = FMSTP.valueFromJs(value, "clear", "false").toBool();

            // pattern
            string group = FMSTP.valueFromJs(value, "pattern", "").toString();
            group_type = ActPlanting.planting_pattern_names.IndexOf(group);
            if (String.IsNullOrEmpty(group) == false && group != "undefined" && group_type == -1)
            {
                throw new NotSupportedException(String.Format("Planting-activity: the pattern '{0}' is not valid!", group));
            }
            spacing = FMSTP.valueFromJs(value, "spacing", "0").toInt();
            offset = FMSTP.valueFromJs(value, "offset", "0").toInt();

            bool random = FMSTP.boolValueFromJs(value, "random", false);
            if (random)
            {
                group_random_count = FMSTP.valueFromJs(value, "n", "0").toInt();
            }
            else
            {
                group_random_count = 0;
            }
            grouped = group_type >= 0;
            return true;
        }

        public void run(FMStand stand)
        {
            RectangleF box = ForestManagementEngine.standGrid().boundingBox(stand.id());
            MapGrid sgrid = ForestManagementEngine.standGrid();
            Model model = GlobalSettings.instance().model();
            GridRunner<float> runner = new GridRunner<float>(model.grid(), box);
            if (!grouped)
            {
                // distribute saplings randomly.
                // this adds saplings to SaplingCell (only if enough slots are available)
                for (runner.next(); runner.isValid(); runner.next())
                {
                    if (sgrid.standIDFromLIFCoord(runner.currentIndex()) != stand.id())
                    {
                        continue;
                    }
                    if (RandomGenerator.drandom() < fraction)
                    {
                        ResourceUnit ru = model.ru(runner.currentCoord());
                        ru.saplingCell(runner.currentIndex()).addSapling((float)height, age, species.index());
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
                    runner.reset();
                    for (runner.next(); runner.isValid(); runner.next())
                    {
                        Point qp = runner.currentIndex();
                        if (sgrid.standIDFromLIFCoord(qp) != stand.id())
                        {
                            continue;
                        }
                        // check location in the pre-defined planting patterns
                        int idx = (qp.X + offset) % n + n * ((qp.Y + offset) % n);
                        if (pp[idx] == '1')
                        {
                            ResourceUnit ru = model.ru(runner.currentCoord());
                            SaplingCell sc = ru.saplingCell(qp);

                            if (clear)
                            {
                                // clear all sapling trees on the cell
                                model.saplings().clearSaplings(sc, ru, true);
                            }
                            sc.addSapling((float)height, age, species.index());
                        }
                    }
                }
                else
                {
                    // pattern based (with spacing / offset, random...)
                    int ispacing = spacing / Constant.cPxSize;
                    Point p = model.grid().indexAt(box.TopLeft()).Subtract(new Point(offset, offset));
                    Point pstart = p;
                    Point p_end = model.grid().indexAt(box.BottomRight());
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
                            p = model.grid().indexAt(new PointF((float)RandomGenerator.nrandom(box.Left, box.Right), (float)RandomGenerator.nrandom(box.Top, box.Bottom)));
                        }

                        // apply the pattern....
                        for (int y = 0; y < n; ++y)
                        {
                            for (int x = 0; x < n; ++x)
                            {
                                po = p.Add(new Point(x, y));
                                if (sgrid.standIDFromLIFCoord(po) != stand.id())
                                {
                                    continue;
                                }
                                ResourceUnit ru = model.ru(model.grid().cellCenterPoint(po));
                                SaplingCell sc = ru.saplingCell(po);

                                if (clear)
                                {
                                    // clear all sapling trees
                                    model.saplings().clearSaplings(sc, ru, true);
                                }
                                sc.addSapling((float)height, age, species.index());
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
