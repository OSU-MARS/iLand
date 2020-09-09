using iLand.core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.tools
{
    /**
     * @brief The SpatialAnalysis class is the scripting class related to extra spatial analysis functions.
     * rumpleIndex: ratio crown surface area / ground area for the whole project area.
     * saveRumpleGrid(): save RU-based grid of rumple indices to an ASCII grid file.
     */
    internal class SpatialAnalysis
    {
        private RumpleIndex mRumple;
        private SpatialLayeredGrid mLayers;
        private Grid<float> mCrownCoverGrid;
        private Grid<int> mClumpGrid;
        private List<int> mLastPatches;

        public SpatialAnalysis(object parent = null)
        {
            // BUGBUG: parent not ported to C# - does this matteR?
            // QObject(parent)
            mRumple = null;
        }

        public List<int> patchsizes() { return mLastPatches; }

        public static void addToScriptEngine()
        {
            SpatialAnalysis spati = new SpatialAnalysis();
            QJSValue v = GlobalSettings.instance().scriptEngine().newQObject(spati);
            GlobalSettings.instance().scriptEngine().globalObject().setProperty("SpatialAnalysis", v);
        }

        public double rumpleIndexFullArea()
        {
            if (mRumple == null)
            {
                mRumple = new RumpleIndex();
            }
            double rum = mRumple.value();
            return rum;
        }

        /// extract patches (clumps) from the grid 'src'.
        /// Patches are defined as adjacent pixels (8-neighborhood)
        /// Return: vector with number of pixels per patch (first element: patch 1, second element: patch 2, ...)
        public List<int> extractPatches(Grid<double> src, int min_size, string fileName)
        {
            mClumpGrid.setup(src.metricRect(), src.cellsize());
            mClumpGrid.wipe();

            // now loop over all pixels and run a floodfill algorithm
            Point start;
            Queue<Point> pqueue = new Queue<Point>(); // for the flood fill algorithm
            List<int> counts = new List<int>();
            int patch_index = 0;
            int total_size = 0;
            int patches_skipped = 0;
            for (int i = 0; i < src.count(); ++i)
            {
                if (src[i] > 0.0 && mClumpGrid[i] == 0)
                {
                    start = src.indexOf(i);
                    pqueue.Clear();
                    patch_index++;

                    // quick and dirty implementation of the flood fill algroithm.
                    // based on: http://en.wikipedia.org/wiki/Flood_fill
                    // returns the number of pixels colored
                    pqueue.Enqueue(start);
                    int found = 0;
                    while (pqueue.Count > 0)
                    {
                        Point p = pqueue.Dequeue();
                        if (!src.isIndexValid(p))
                            continue;
                        if (src.valueAtIndex(p) > 0.0 && mClumpGrid.valueAtIndex(p) == 0)
                        {
                            mClumpGrid[p] = patch_index;
                            pqueue.Enqueue(new Point(p.X - 1, p.Y));
                            pqueue.Enqueue(new Point(p.X + 1, p.Y));
                            pqueue.Enqueue(new Point(p.X, p.Y - 1));
                            pqueue.Enqueue(new Point(p.X, p.Y + 1));
                            pqueue.Enqueue(new Point(p.X + 1, p.Y + 1));
                            pqueue.Enqueue(new Point(p.X - 1, p.Y + 1));
                            pqueue.Enqueue(new Point(p.X - 1, p.Y - 1));
                            pqueue.Enqueue(new Point(p.X + 1, p.Y - 1));
                            ++found;
                        }
                    }
                    if (found < min_size)
                    {
                        // delete the patch again
                        pqueue.Enqueue(start);
                        while (pqueue.Count > 0)
                        {
                            Point p = pqueue.Dequeue();
                            if (!src.isIndexValid(p))
                            {
                                continue;
                            }
                            if (mClumpGrid[p] == patch_index)
                            {
                                mClumpGrid[p] = -1;
                                pqueue.Enqueue(new Point(p.X - 1, p.Y));
                                pqueue.Enqueue(new Point(p.X + 1, p.Y));
                                pqueue.Enqueue(new Point(p.X, p.Y - 1));
                                pqueue.Enqueue(new Point(p.X, p.Y + 1));
                                pqueue.Enqueue(new Point(p.X + 1, p.Y + 1));
                                pqueue.Enqueue(new Point(p.X - 1, p.Y + 1));
                                pqueue.Enqueue(new Point(p.X - 1, p.Y - 1));
                                pqueue.Enqueue(new Point(p.X + 1, p.Y - 1));
                            }
                        }
                        --patch_index;
                        patches_skipped++;
                    }
                    else
                    {
                        // save the patch in the result
                        counts.Add(found);
                        total_size += found;
                    }
                }
            }
            // remove the -1 again...
            mClumpGrid.limit(0, 999999);

            Debug.WriteLine("extractPatches: found " + patch_index + " patches, total valid pixels: " + total_size + " skipped" + patches_skipped);
            if (String.IsNullOrEmpty(fileName) == false)
            {
                Debug.WriteLine("extractPatches: save to file: " + GlobalSettings.instance().path(fileName));
                Helper.saveToTextFile(GlobalSettings.instance().path(fileName), Grid.gridToESRIRaster(mClumpGrid));
            }
            return counts;

        }

        public void saveRumpleGrid(string fileName)
        {
            if (mRumple == null)
            {
                mRumple = new RumpleIndex();
            }
            Helper.saveToTextFile(GlobalSettings.instance().path(fileName), Grid.gridToESRIRaster(mRumple.rumpleGrid()));
        }

        public void saveCrownCoverGrid(string fileName)
        {
            calculateCrownCover();
            Helper.saveToTextFile(GlobalSettings.instance().path(fileName), Grid.gridToESRIRaster(mCrownCoverGrid));
        }

        public QJSValue patches(QJSValue grid, int min_size)
        {
            ScriptGrid sg = (ScriptGrid)grid.toQObject();
            if (sg != null)
            {
                // extract patches (keep patches with a size >= min_size
                mLastPatches = extractPatches(sg.grid(), min_size, String.Empty);
                // create a (double) copy of the internal clump grid, and return this grid
                // as a JS value
                QJSValue v = ScriptGrid.createGrid(mClumpGrid.toDouble(), "patch");
                return v;
            }
            return new QJSValue();
        }

        private void calculateCrownCover()
        {
            mCrownCoverGrid.setup(GlobalSettings.instance().model().RUgrid().metricRect(),
                                  GlobalSettings.instance().model().RUgrid().cellsize());

            // calculate the crown cover per resource unit. We use the "reader"-stamps of the individual trees
            // as they represent the crown (size). We also simply hijack the LIF grid for our calculations.
            Grid<float> grid = GlobalSettings.instance().model().grid();
            grid.initialize(0.0F);
            // we simply iterate over all trees of all resource units (not bothering about multithreading here)
            AllTreeIterator ati = new AllTreeIterator(GlobalSettings.instance().model());
            for (Tree t = ati.nextLiving(); t != null; t = ati.nextLiving())
            {
                // apply the reader-stamp
                Stamp reader = t.stamp().reader();
                Point pos_reader = t.positionIndex(); // tree position
                pos_reader.X -= reader.offset();
                pos_reader.Y -= reader.offset();
                int reader_size = reader.size();
                int rx = pos_reader.X;
                int ry = pos_reader.Y;
                // the reader stamps are stored such as to have a sum of 1.0 over all pixels
                // (i.e.: they express the percentage for each cell contributing to the full crown).
                // we thus calculate a the factor to "blow up" cell values; a fully covered cell has then a value of 1,
                // and values between 0-1 are cells that are partially covered by the crown.
                double crown_factor = reader.crownArea() / (double)(Constant.cPxSize * Constant.cPxSize);

                // add the reader-stamp values: multiple (partial) crowns can add up to being fully covered
                for (int y = 0; y < reader_size; ++y)
                {
                    for (int x = 0; x < reader_size; ++x)
                    {
                        grid[rx + x, ry + y] += (float)(reader[x, y] * crown_factor);
                    }
                }
            }
            // now aggregate values for each resource unit
            Model model = GlobalSettings.instance().model();
            for (int rg = 0; rg < mCrownCoverGrid.count(); ++rg)
            {
                ResourceUnit ru = model.RUgrid().constValueAtIndex(mCrownCoverGrid.indexOf(rg));
                if (ru == null)
                {
                    mCrownCoverGrid[rg] = 0.0F;
                    continue;
                }
                float cc_sum = 0.0F;
                GridRunner<float> runner = new GridRunner<float>(grid, mCrownCoverGrid.cellRect(mCrownCoverGrid.indexOf(rg)));
                for (runner.next(); runner.isValid(); runner.next())
                {
                    float gv = runner.current();
                    if (model.heightGridValue(runner.currentIndex().X, runner.currentIndex().Y).isValid())
                    {
                        if (gv >= 0.5F) // 0.5: half of a 2m cell is covered by a tree crown; is a bit pragmatic but seems reasonable (and works)
                        {
                            cc_sum++;
                        }
                    }
                }
                if (ru.stockableArea() > 0.0)
                {
                    double value = Constant.cPxSize * Constant.cPxSize * cc_sum / ru.stockableArea();
                    mCrownCoverGrid[rg] = (float)Global.limit(value, 0.0, 1.0);
                }
            }
        }
    }
}
