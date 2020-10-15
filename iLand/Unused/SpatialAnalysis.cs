using iLand.Simulation;
using iLand.Trees;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace iLand.Tools
{
    /**
     * @brief The SpatialAnalysis class is the scripting class related to extra spatial analysis functions.
     * rumpleIndex: ratio crown surface area / ground area for the whole project area.
     * saveRumpleGrid(): save RU-based grid of rumple indices to an ASCII grid file.
     */
    internal class SpatialAnalysis
    {
        private RumpleIndex mRumple;
        private readonly Grid<float> mCrownCoverGrid;
        private readonly Grid<int> mClumpGrid;

        public List<int> PatchSizes { get; private set; }

        public SpatialAnalysis()
        {
            // BUGBUG: parent not ported to C# - does this matteR?
            // QObject(parent)
            this.mClumpGrid = new Grid<int>();
            this.mCrownCoverGrid = new Grid<float>();
            this.mRumple = null;
        }

        public double RumpleIndexFullArea(Model model)
        {
            if (mRumple == null)
            {
                mRumple = new RumpleIndex();
            }
            double rum = mRumple.Value(model);
            return rum;
        }

        /// extract patches (clumps) from the grid 'src'.
        /// Patches are defined as adjacent pixels (8-neighborhood)
        /// Return: vector with number of pixels per patch (first element: patch 1, second element: patch 2, ...)
        public List<int> ExtractPatches(Model model, Grid<double> src, int min_size, string fileName)
        {
            mClumpGrid.Setup(src.PhysicalExtent, src.CellSize);
            mClumpGrid.ClearDefault();

            // now loop over all pixels and run a floodfill algorithm
            Point start;
            Queue<Point> pqueue = new Queue<Point>(); // for the flood fill algorithm
            List<int> counts = new List<int>();
            int patch_index = 0;
            int total_size = 0;
            int patches_skipped = 0;
            for (int i = 0; i < src.Count; ++i)
            {
                if (src[i] > 0.0 && mClumpGrid[i] == 0)
                {
                    start = src.IndexOf(i);
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
                        if (!src.Contains(p))
                            continue;
                        if (src[p] > 0.0 && mClumpGrid[p] == 0)
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
                            if (!src.Contains(p))
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
            mClumpGrid.Limit(0, 999999);

            Debug.WriteLine("extractPatches: found " + patch_index + " patches, total valid pixels: " + total_size + " skipped" + patches_skipped);
            if (String.IsNullOrEmpty(fileName) == false)
            {
                Debug.WriteLine("extractPatches: save to file: " + model.GlobalSettings.Path(fileName));
                File.WriteAllText(model.GlobalSettings.Path(fileName), Grid.ToEsriRaster(model, mClumpGrid));
            }
            return counts;

        }

        public void SaveRumpleGrid(Model model, string fileName)
        {
            if (mRumple == null)
            {
                mRumple = new RumpleIndex();
            }
            File.WriteAllText(model.GlobalSettings.Path(fileName), Grid.ToEsriRaster(model, mRumple.RumpleGrid(model)));
        }

        public void SaveCrownCoverGrid(Model model, string fileName)
        {
            CalculateCrownCover(model);
            File.WriteAllText(model.GlobalSettings.Path(fileName), Grid.ToEsriRaster(model, mCrownCoverGrid));
        }

        private void CalculateCrownCover(Model model)
        {
            mCrownCoverGrid.Setup(model.ResourceUnitGrid.PhysicalExtent, model.ResourceUnitGrid.CellSize);

            // calculate the crown cover per resource unit. We use the "reader"-stamps of the individual trees
            // as they represent the crown (size). We also simply hijack the LIF grid for our calculations.
            Grid<float> grid = model.LightGrid;
            grid.Initialize(0.0F);
            // we simply iterate over all trees of all resource units (not bothering about multithreading here)
            AllTreeIterator ati = new AllTreeIterator(model);
            for (Tree t = ati.MoveNextLiving(); t != null; t = ati.MoveNextLiving())
            {
                // apply the reader-stamp
                Stamp reader = t.Stamp.Reader;
                Point pos_reader = t.LightCellPosition; // tree position
                pos_reader.X -= reader.CenterCellPosition;
                pos_reader.Y -= reader.CenterCellPosition;
                int reader_size = reader.Size();
                int rx = pos_reader.X;
                int ry = pos_reader.Y;
                // the reader stamps are stored such as to have a sum of 1.0 over all pixels
                // (i.e.: they express the percentage for each cell contributing to the full crown).
                // we thus calculate a the factor to "blow up" cell values; a fully covered cell has then a value of 1,
                // and values between 0-1 are cells that are partially covered by the crown.
                double crown_factor = reader.CrownArea / (double)(Constant.LightSize * Constant.LightSize);

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
            for (int crownIndex = 0; crownIndex < mCrownCoverGrid.Count; ++crownIndex)
            {
                ResourceUnit ru = model.ResourceUnitGrid[mCrownCoverGrid.IndexOf(crownIndex)];
                if (ru == null)
                {
                    mCrownCoverGrid[crownIndex] = 0.0F;
                    continue;
                }
                int cellsWithCrownCoverage = 0;
                GridRunner<float> coverRunner = new GridRunner<float>(grid, mCrownCoverGrid.GetCellRect(mCrownCoverGrid.IndexOf(crownIndex)));
                for (coverRunner.MoveNext(); coverRunner.IsValid(); coverRunner.MoveNext())
                {
                    float canopyCover = coverRunner.Current;
                    if (model.HeightGridValue(coverRunner.CurrentIndex().X, coverRunner.CurrentIndex().Y).IsInWorld())
                    {
                        if (canopyCover >= 0.5F) // 0.5: half of a 2m cell is covered by a tree crown; is a bit pragmatic but seems reasonable (and works)
                        {
                            // TODO: why not sum the canopy cover?
                            cellsWithCrownCoverage++;
                        }
                    }
                }
                if (ru.StockableArea > 0.0)
                {
                    double value = Constant.LightSize * Constant.LightSize * cellsWithCrownCoverage / ru.StockableArea;
                    mCrownCoverGrid[crownIndex] = (float)Global.Limit(value, 0.0, 1.0);
                }
            }
        }
    }
}
