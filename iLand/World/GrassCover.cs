﻿// C++/core/{ grasscover.h, grasscover.cpp }
using iLand.Input.ProjectFile;
using iLand.Tool;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.World
{
    // TODO: remove as class becomes a wrapper for ResourceUnit.simlifiedGrassCover() / Saplings::simplifiedGrassCover() in iLand 2.0
    // TODO: would be cleaner if split into two classes with virtual method overrides by algorithm
    public class GrassCover
    {
        // cover is encoded using not quite Q1.15 fixed point
        private const int FullCoverValue = 32000;

        private GrassAlgorithm algorithm; // C++ mode(), mType
        private readonly RandomCustomPdf cellProbabilityDensityFunction; // probability density function defining the life time of grass-pixels
        private readonly Expression continousCover; // function defining max. grass cover [0..1] as function of the LIF pixel value
        private Int16 continuousCoverAtFullLight; // potential at lif=1
        private int continousGrowthRate; // max. annual growth rate of herbs and grasses (in 1/256th)
        private int continuousYearsToFullCover; // maximum duration (years) from 0 to full cover
        private readonly float[] continuousRegenerationEffectByCover; // effect lookup table

        //private readonly GrassCoverLayers mLayers; // visualization

        public float CellLifThreshold { get; private set; } // if LIF>threshold, then the grass is considered as occupatied by grass, C++ lifThreshold(), mGrassLIFThreshold

        // grid of current grass cover, same cell size as light grid
        // cell on-off mode: 0 = pixel enabled for regeneration, n > 1 = pixel disabled for regeneration for next n years
        // continuous mode: 0 = no cover in light pixel, Q15MaxValue = 100% cover in light pixel
        public Grid<Int16> CoverOrOnOffGrid { get; private init; }
        public bool IsEnabled { get; private set; }

        public GrassCover()
        {
            this.algorithm = GrassAlgorithm.Simplified;
            this.cellProbabilityDensityFunction = new();
            this.continousCover = new();
            this.continuousRegenerationEffectByCover = new float[GrassCover.FullCoverValue];
            //this.mLayers = new();

            this.CoverOrOnOffGrid = new();
            this.IsEnabled = false;

            //this.mLayers.SetGrid(this.Grid);
        }

        //public float GetCoverFraction(Int16 coverLevel) { return this.algorithm == GrassAlgorithm.LightPixelOnOff ? coverLevel : coverLevel / (float)(GrassCover.Steps - 1); }
        // access values of the underlying grid (for visualization)
        //public float GetRegenerationEffect(Int16 coverLevel) { return this.continuousRegenerationEffectByCover[coverLevel]; }

        // TODO: should be connected to seedling establishment
        //public float GetRegenerationInhibition(Point lightCellIndex)
        //{
        //    if (mType == GrassAlgorithmType.Pixel)
        //    {
        //        // -1: off, out of project area, 0: off, ready to get grassy again, 1: off (waiting for LIF threshold), >1 on, counting down
        //        return this.Grid[lightCellIndex] > 1 ? 1.0 : 0.0;
        //    }
        //    // type continuous
        //    return this.IsEnabled ? this.Effect(Grid[lightCellIndex]) : 0.0;
        //}

        public void Setup(Project projectFile, Landscape landscape) // C++: GrassCover::setup()
        {
            if (projectFile.World.Grass.Enabled == false)
            {
                this.IsEnabled = false;
                return;
            }

            this.algorithm = projectFile.World.Grass.Algorithm;
            if (this.algorithm != GrassAlgorithm.Simplified)
            {
                throw new NotSupportedException("Currently only the simplified grass algorithm is supported.");
            }

            if (this.algorithm == GrassAlgorithm.Pixel)
            {
                // setup of pixel based / discrete approach
                string? durationFormula = projectFile.World.Grass.PixelDuration;
                if (String.IsNullOrEmpty(durationFormula))
                {
                    throw new NotSupportedException("Missing equation for 'grassDuration'.");
                }
                this.cellProbabilityDensityFunction.Setup(durationFormula, 0.0F, 100.0F);
                //mGrassEffect.setExpression(formula);
                this.CellLifThreshold = projectFile.World.Grass.PixelLifThreshold;

                // clear array
                for (int stepIndex = 0; stepIndex < GrassCover.FullCoverValue; ++stepIndex)
                {
                    this.continuousRegenerationEffectByCover[stepIndex] = 0.0F;
                }
            }
            else if (this.algorithm == GrassAlgorithm.ContinuousLight)
            {
                // setup of continuous grass concept
                string? grassPotential = projectFile.World.Grass.ContinuousCover;
                if (String.IsNullOrEmpty(grassPotential))
                {
                    throw new NotSupportedException("Required expression 'grassPotential' is missing.");
                }
                this.continousCover.SetExpression(grassPotential);
                this.continousCover.Linearize(0.0F, 1.0F, Math.Min(GrassCover.FullCoverValue, 1000));

                string? grassEffect = projectFile.World.Grass.ContinuousRegenerationEffect;
                if (String.IsNullOrEmpty(grassEffect))
                {
                    throw new NotSupportedException("Required expression 'grassEffect' is missing.");
                }
                Expression continousRegenerationEffect = new(grassEffect);

                this.continuousYearsToFullCover = projectFile.World.Grass.ContinuousYearsToFullCover;
                if (this.continuousYearsToFullCover == 0)
                {
                    throw new NotSupportedException("Value of 'maxTimeLag' is invalid or missing.");
                }
                this.continousGrowthRate = GrassCover.FullCoverValue / this.continuousYearsToFullCover;

                // set up the effect on regeneration in NSTEPS steps
                for (int stepIndex = 0; stepIndex < GrassCover.FullCoverValue; ++stepIndex)
                {
                    float regenEffect = continousRegenerationEffect.Evaluate(stepIndex / (float)(GrassCover.FullCoverValue - 1));
                    this.continuousRegenerationEffectByCover[stepIndex] = Maths.Limit(regenEffect, 0.0F, 1.0F);
                }

                this.continuousCoverAtFullLight = (Int16)(Maths.Limit(continousCover.Evaluate(1.0F), 0.0F, 1.0F) * (GrassCover.FullCoverValue - 1)); // the max value of the potential function
            }
            else if (this.algorithm == GrassAlgorithm.Simplified)
            {
                // nothing to do
            }
            else
            {
                throw new NotSupportedException("Unhandled algorithm " + this.algorithm + ".");
            }

            // create the grid
            this.CoverOrOnOffGrid.Setup(landscape.LightGrid.ProjectExtent, landscape.LightGrid.CellSizeInM);
            this.CoverOrOnOffGrid.Fill(-1); // default all grass cells to being outside of a resource unit
            for (int resourceUnitIndex = 0; resourceUnitIndex < landscape.ResourceUnits.Count; ++resourceUnitIndex)
            {
                ResourceUnit resourceUnit = landscape.ResourceUnits[resourceUnitIndex];
                Point seedmapIndexXY = this.CoverOrOnOffGrid.GetCellXYIndex(resourceUnit.ProjectExtent.X, resourceUnit.ProjectExtent.Y);
                this.CoverOrOnOffGrid.Fill(seedmapIndexXY.X, seedmapIndexXY.Y, Constant.Grid.LightCellsPerRUWidth, Constant.Grid.LightCellsPerRUWidth, 0);
            }

            this.IsEnabled = true;
            // Debug.WriteLine("setup of grass cover complete.");
        }

        public void SetInitialValues(RandomGenerator randomGenerator, List<(int CellIndex, float LightValue)> lightCells, int percentGrassCover) // C++: GrassCover::setInitialValues()
        {
            Debug.Assert((percentGrassCover >= 0) && (percentGrassCover <= 100));
            if (this.IsEnabled == false)
            {
                return;
            }
            if (algorithm == GrassAlgorithm.ContinuousLight)
            {
                Int16 grassValue = (Int16)(0.01F * percentGrassCover * (GrassCover.FullCoverValue - 1));
                if (grassValue > continuousCoverAtFullLight)
                {
                    grassValue = continuousCoverAtFullLight;
                }
                for (int lightCell = 0; lightCell < lightCells.Count; ++lightCell)
                {
                    this.CoverOrOnOffGrid[lightCells[lightCell].CellIndex] = grassValue;
                }
            }
            else if (algorithm == GrassAlgorithm.Pixel)
            {
                for (int lightCell = 0; lightCell < lightCells.Count; ++lightCell)
                {
                    if (percentGrassCover > randomGenerator.GetRandomInteger(0, 100))
                    {
                        this.CoverOrOnOffGrid[lightCells[lightCell].CellIndex] = (Int16)this.cellProbabilityDensityFunction.GetRandomValue(randomGenerator);
                    }
                    else
                    {
                        this.CoverOrOnOffGrid[lightCells[lightCell].CellIndex] = 0;
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Unhandled algorithm " + this.algorithm + ".");
            }
        }

        /// main function (growth/die-off of grass cover)
        public void UpdateCoverage(Landscape landscape, RandomGenerator randomGenerator) // C++: GrassCover::execute()
        {
            if ((this.IsEnabled == false) || (this.algorithm == GrassAlgorithm.Simplified))
            {
                return;
            }

            // main function of the grass submodule
            Grid<float> lightGrid = landscape.LightGrid;
            if (algorithm == GrassAlgorithm.ContinuousLight)
            {
                // loop over every light pixel
                //int cellsSkipped = 0;
                for (int lightIndex = 0; lightIndex != lightGrid.CellCount; ++lightIndex)
                {
                    // calculate potential grass vegetation cover
                    if (lightGrid[lightIndex] == 1.0F && this.CoverOrOnOffGrid[lightIndex] == continuousCoverAtFullLight)
                    {
                        //++cellsSkipped;
                        continue;
                    }

                    int maxCover = (int)(Maths.Limit(this.continousCover.Evaluate(lightGrid[lightIndex]), 0.0F, 1.0F) * (GrassCover.FullCoverValue - 1));
                    this.CoverOrOnOffGrid[lightIndex] = (Int16)Math.Min(this.CoverOrOnOffGrid[lightIndex] + this.continousGrowthRate, maxCover);
                }
                // Debug.WriteLine("skipped " << cellsSkipped);
            }
            else if (algorithm == GrassAlgorithm.Pixel)
            {
                for (int lightIndex = 0; lightIndex < lightGrid.CellCount; ++lightIndex)
                {
                    int grassCover = this.CoverOrOnOffGrid[lightIndex];
                    Debug.Assert(grassCover >= -1);

                    if (grassCover > 1)
                    {
                        --this.CoverOrOnOffGrid[lightIndex]; // count down the years (until gr=1)
                    }
                    else if ((grassCover == 0) && (lightGrid[lightIndex] > this.CellLifThreshold))
                    {
                        // enable grass cover
                        this.CoverOrOnOffGrid[lightIndex] = (Int16)(MathF.Max(this.cellProbabilityDensityFunction.GetRandomValue(randomGenerator), 0.0F) + 1); // switch on...
                    }
                    else if ((grassCover == 1) && (lightGrid[lightIndex] < this.CellLifThreshold))
                    {
                        // now LIF is below the threshold - this enables the pixel to get grassy again
                        this.CoverOrOnOffGrid[lightIndex] = 0;
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Unhandled algorithm " + this.algorithm + ".");
            }
        }
    }
}
