﻿using iLand.Input.ProjectFile;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using Model = iLand.Simulation.Model;

namespace iLand.Tools
{
    /**
     * @brief The SpatialAnalysis class is the scripting class related to extra spatial analysis functions.
     * rumpleIndex: ratio crown surface area / ground area for the whole project area.
     * saveRumpleGrid(): save RU-based grid of rumple indices to an ASCII grid file.
     */
    internal class SpatialAnalysis
    {
        private RumpleIndex? mRumple;
        private readonly Grid<float> mCrownCoverGrid;
        private readonly Grid<int> mClumpGrid;

        public SpatialAnalysis()
        {
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
            double rum = mRumple.GetIndex(model);
            return rum;
        }

        /// extract patches (clumps) from the grid 'src'.
        /// Patches are defined as adjacent pixels (8-neighborhood)
        /// Return: vector with number of pixels per patch (first element: patch 1, second element: patch 2, ...)
        public List<int> ExtractPatches(Model model, Grid<double> src, int min_size, string fileName)
        {
            mClumpGrid.Setup(src.PhysicalExtent, src.CellSize);
            mClumpGrid.FillDefault();

            // now loop over all pixels and run a floodfill algorithm
            Point start;
            Queue<Point> pqueue = new(); // for the flood fill algorithm
            List<int> counts = new();
            int patch_index = 0;
            int total_size = 0;
            int patches_skipped = 0;
            for (int i = 0; i < src.Count; ++i)
            {
                if (src[i] > 0.0 && mClumpGrid[i] == 0)
                {
                    start = src.GetCellPosition(i);
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
                Debug.WriteLine("extractPatches: save to file: " + model.Project.GetFilePath(ProjectDirectory.Home, fileName));
                File.WriteAllText(model.Project.GetFilePath(ProjectDirectory.Home, fileName), Grid.ToEsriRaster(model.Landscape, mClumpGrid));
            }
            return counts;

        }

        public void SaveRumpleGrid(Model model, string fileName)
        {
            if (mRumple == null)
            {
                mRumple = new RumpleIndex();
            }
            File.WriteAllText(model.Project.GetFilePath(ProjectDirectory.Home, fileName), Grid.ToEsriRaster(model.Landscape, mRumple.GetRumpleGrid(model)));
        }

        public void SaveCrownCoverGrid(Model model, string fileName)
        {
            CalculateCrownCover(model);
            File.WriteAllText(model.Project.GetFilePath(ProjectDirectory.Home, fileName), Grid.ToEsriRaster(model.Landscape, mCrownCoverGrid));
        }

        private void CalculateCrownCover(Model model)
        {
            mCrownCoverGrid.Setup(model.Landscape.ResourceUnitGrid.PhysicalExtent, model.Landscape.ResourceUnitGrid.CellSize);

            // calculate the crown cover per resource unit. We use the "reader"-stamps of the individual trees
            // as they represent the crown (size). We also simply hijack the LIF grid for our calculations.
            Grid<float> crownCoverGrid = new(model.Landscape.LightGrid);
            crownCoverGrid.Fill(0.0F);
            // we simply iterate over all trees of all resource units (not bothering about multithreading here)
            AllTreesEnumerator allTreeEnumerator = new(model.Landscape);
            while (allTreeEnumerator.MoveNextLiving())
            {
                // apply the reader-stamp
                Trees trees = allTreeEnumerator.CurrentTrees;
                LightStamp reader = trees.Stamp[allTreeEnumerator.CurrentTreeIndex]!.Reader!;
                Point readerOrigin = trees.LightCellPosition[allTreeEnumerator.CurrentTreeIndex]; // tree position
                readerOrigin.X -= reader.CenterCellPosition;
                readerOrigin.Y -= reader.CenterCellPosition;
                int readerSize = reader.Size();
                int readerOriginX = readerOrigin.X;
                int readerOriginY = readerOrigin.Y;
                // the reader stamps are stored such as to have a sum of 1.0 over all pixels
                // (i.e.: they express the percentage for each cell contributing to the full crown).
                // we thus calculate a the factor to "blow up" cell values; a fully covered cell has then a value of 1,
                // and values between 0-1 are cells that are partially covered by the crown.
                float crownAreaInLightCells = reader.CrownArea / (Constant.LightSize * Constant.LightSize);

                // add the reader-stamp values: multiple (partial) crowns can add up to being fully covered
                for (int y = 0; y < readerSize; ++y)
                {
                    for (int x = 0; x < readerSize; ++x)
                    {
                        crownCoverGrid[readerOriginX + x, readerOriginY + y] += reader[x, y] * crownAreaInLightCells;
                    }
                }
            }
            // now aggregate values for each resource unit
            for (int crownIndex = 0; crownIndex < mCrownCoverGrid.Count; ++crownIndex)
            {
                ResourceUnit ru = model.Landscape.ResourceUnitGrid[mCrownCoverGrid.GetCellPosition(crownIndex)];
                if (ru == null)
                {
                    mCrownCoverGrid[crownIndex] = 0.0F;
                    continue;
                }
                int cellsWithCrownCoverage = 0;
                GridWindowEnumerator<float> coverRunner = new(crownCoverGrid, mCrownCoverGrid.GetCellExtent(mCrownCoverGrid.GetCellPosition(crownIndex)));
                while (coverRunner.MoveNext())
                {
                    float canopyCover = coverRunner.Current;
                    if (model.Landscape.HeightGrid[coverRunner.GetCellPosition().X, coverRunner.GetCellPosition().Y, Constant.LightCellsPerHeightSize].IsOnLandscape())
                    {
                        if (canopyCover >= 0.5F) // 0.5: half of a 2m cell is covered by a tree crown; is a bit pragmatic but seems reasonable (and works)
                        {
                            // TODO: why not sum the canopy cover?
                            cellsWithCrownCoverage++;
                        }
                    }
                }
                if (ru.AreaInLandscape > 0.0F)
                {
                    float value = Constant.LightSize * Constant.LightSize * cellsWithCrownCoverage / ru.AreaInLandscape;
                    mCrownCoverGrid[crownIndex] = Maths.Limit(value, 0.0F, 1.0F);
                }
            }
        }
    }
}
