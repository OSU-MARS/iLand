using iLand.Input.ProjectFile;
using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.World
{
    // TODO: would be cleaner if split into two classes with virtual method overrides by algorithm
    public class GrassCover
    {
        // cover is encoded using not quite Q1.15 fixed point
        private const int FullCoverValue = 32000;

        private GrassAlgorithm algorithm;
        private readonly RandomCustomPdf cellProbabilityDensityFunction; // probability density function defining the life time of grass-pixels
        private float cellLifThreshold; // if LIF>threshold, then the grass is considered as occupatied by grass
        private readonly Expression continousCover; // function defining max. grass cover [0..1] as function of the LIF pixel value
        private Int16 continuousCoverAtFullLight; // potential at lif=1
        private int continousGrowthRate; // max. annual growth rate of herbs and grasses (in 1/256th)
        private int continuousYearsToFullCover; // maximum duration (years) from 0 to full cover
        private readonly float[] continuousRegenerationEffectByCover; // effect lookup table

        //private readonly GrassCoverLayers mLayers; // visualization

        // grid of current grass cover, same cell size as light grid
        // cell on-off mode: 0 = pixel enabled for regeneration, n > 1 = pixel disabled for regeneration for next n years
        // continuous mode: 0 = no cover in light pixel, Q15MaxValue = 100% cover in light pixel
        public Grid<Int16> CoverOrOnOffGrid { get; private init; }
        public bool IsEnabled { get; private set; }

        public GrassCover()
        {
            this.algorithm = GrassAlgorithm.CellOnOff;
            this.cellProbabilityDensityFunction = new RandomCustomPdf();
            this.continousCover = new Expression();
            this.continuousRegenerationEffectByCover = new float[GrassCover.FullCoverValue];
            //this.mLayers = new GrassCoverLayers();

            this.CoverOrOnOffGrid = new Grid<short>();
            this.IsEnabled = false;

            //this.mLayers.SetGrid(this.Grid);
        }

        //public float GetCoverFraction(Int16 coverLevel) { return this.algorithm == GrassAlgorithm.LightPixelOnOff ? coverLevel : coverLevel / (float)(GrassCover.Steps - 1); }
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

        public void Setup(Project projectFile, Landscape landscape)
        {
            if (projectFile.World.Grass.Enabled == false)
            {
                // clear grid
                this.CoverOrOnOffGrid.Clear();
                this.IsEnabled = false;
                // Debug.WriteLine("grass module not enabled");
                return;
            }

            // create the grid
            this.CoverOrOnOffGrid.Setup(landscape.LightGrid.PhysicalExtent, landscape.LightGrid.CellSize);
            this.CoverOrOnOffGrid.FillDefault();
            // mask out out-of-project areas
            for (int lightIndex = 0; lightIndex < this.CoverOrOnOffGrid.Count; ++lightIndex)
            {
                if (landscape.HeightGrid[this.CoverOrOnOffGrid.Index5(lightIndex)].IsOnLandscape() == false)
                {
                    this.CoverOrOnOffGrid[lightIndex] = -1;
                }
            }

            this.algorithm = projectFile.World.Grass.Algorithm;
            if (this.algorithm == GrassAlgorithm.CellOnOff)
            {
                // setup of pixel based / discrete approach
                string? durationFormula = projectFile.World.Grass.PixelDuration;
                if (String.IsNullOrEmpty(durationFormula))
                {
                    throw new NotSupportedException("Missing equation for 'grassDuration'.");
                }
                this.cellProbabilityDensityFunction.Setup(durationFormula, 0.0F, 100.0F);
                //mGrassEffect.setExpression(formula);
                this.cellLifThreshold = projectFile.World.Grass.PixelLifThreshold;

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
                this.continousCover.Linearize(0.0, 1.0, Math.Min(GrassCover.FullCoverValue, 1000));

                string? grassEffect = projectFile.World.Grass.ContinuousRegenerationEffect;
                if (String.IsNullOrEmpty(grassEffect))
                {
                    throw new NotSupportedException("Required expression 'grassEffect' is missing.");
                }
                Expression continousRegenerationEffect = new Expression(grassEffect);

                this.continuousYearsToFullCover = projectFile.World.Grass.ContinuousYearsToFullCover;
                if (this.continuousYearsToFullCover == 0)
                {
                    throw new NotSupportedException("Value of 'maxTimeLag' is invalid or missing.");
                }
                this.continousGrowthRate = GrassCover.FullCoverValue / this.continuousYearsToFullCover;

                // set up the effect on regeneration in NSTEPS steps
                for (int stepIndex = 0; stepIndex < GrassCover.FullCoverValue; ++stepIndex)
                {
                    float regenEffect = (float)continousRegenerationEffect.Evaluate(stepIndex / (double)(GrassCover.FullCoverValue - 1));
                    this.continuousRegenerationEffectByCover[stepIndex] = Maths.Limit(regenEffect, 0.0F, 1.0F);
                }

                this.continuousCoverAtFullLight = (Int16)(Maths.Limit(continousCover.Evaluate(1.0F), 0.0, 1.0) * (GrassCover.FullCoverValue - 1)); // the max value of the potential function
            }
            else
            {
                throw new NotSupportedException("Unhandled algorithm " + this.algorithm + ".");
            }

            this.IsEnabled = true;
            // Debug.WriteLine("setup of grass cover complete.");
        }

        public void SetInitialValues(RandomGenerator randomGenerator, List<KeyValuePair<int, float>> lightCells, int percentGrassCover)
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
                    this.CoverOrOnOffGrid[lightCells[lightCell].Key] = grassValue;
                }
            }
            else if (algorithm == GrassAlgorithm.CellOnOff)
            {
                for (int lightCell = 0; lightCell < lightCells.Count; ++lightCell)
                {
                    if (percentGrassCover > randomGenerator.GetRandomInteger(0, 100))
                    {
                        this.CoverOrOnOffGrid[lightCells[lightCell].Key] = (Int16)this.cellProbabilityDensityFunction.GetRandomValue(randomGenerator);
                    }
                    else
                    {
                        this.CoverOrOnOffGrid[lightCells[lightCell].Key] = 0;
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Unhandled algorithm " + this.algorithm + ".");
            }
        }

        public void UpdateCoverage(Landscape landscape, RandomGenerator randomGenerator)
        {
            if (this.IsEnabled == false)
            {
                return;
            }
            //using DebugTimer t = model.DebugTimers.Create("GrassCover.Execute()");

            // Main function of the grass submodule
            Grid<float> lightGrid = landscape.LightGrid;
            if (algorithm == GrassAlgorithm.ContinuousLight)
            {
                // loop over every light pixel
                //int cellsSkipped = 0;
                for (int lightIndex = 0; lightIndex != lightGrid.Count; ++lightIndex)
                {
                    // calculate potential grass vegetation cover
                    if (lightGrid[lightIndex] == 1.0F && this.CoverOrOnOffGrid[lightIndex] == continuousCoverAtFullLight)
                    {
                        //++cellsSkipped;
                        continue;
                    }

                    int maxCover = (int)(Maths.Limit(this.continousCover.Evaluate(lightGrid[lightIndex]), 0.0, 1.0) * (GrassCover.FullCoverValue - 1));
                    this.CoverOrOnOffGrid[lightIndex] = (Int16)Math.Min(this.CoverOrOnOffGrid[lightIndex] + this.continousGrowthRate, maxCover);
                }
                //Debug.WriteLine("skipped " << cellsSkipped);
            }
            else if (algorithm == GrassAlgorithm.CellOnOff)
            {
                for (int lightIndex = 0; lightIndex < lightGrid.Count; ++lightIndex)
                {
                    Debug.Assert(this.CoverOrOnOffGrid[lightIndex] >= 0);
                    if (this.CoverOrOnOffGrid[lightIndex] > 1)
                    {
                        --this.CoverOrOnOffGrid[lightIndex]; // count down the years (until gr=1)
                    }

                    if (this.CoverOrOnOffGrid[lightIndex] == 0 && lightGrid[lightIndex] > this.cellLifThreshold)
                    {
                        // enable grass cover
                        this.CoverOrOnOffGrid[lightIndex] = (Int16)(Math.Max(this.cellProbabilityDensityFunction.GetRandomValue(randomGenerator), 0.0) + 1); // switch on...
                    }
                    if (this.CoverOrOnOffGrid[lightIndex] == 1 && lightGrid[lightIndex] < this.cellLifThreshold)
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
