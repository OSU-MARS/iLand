using iLand.Input.ProjectFile;
using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.World
{
    public class GrassCover
    {
        private const int Steps = 32000;

        private GrassAlgorithmType mType;
        private readonly Expression mGrassPotential; // function defining max. grass cover [0..1] as function of the LIF pixel value
        private readonly Expression mGrassEffect; // equation giving probability of *prohibiting* regeneration as a function of grass level [0..1]
        private int mMaxTimeLag; // maximum duration (years) from 0 to full cover
        private readonly double[] mEffect; // effect lookup table
        private int mGrowthRate; // max. annual growth rate of herbs and grasses (in 1/256th)
        private Int16 mMaxState; // potential at lif=1

        private readonly RandomCustomPdf mPDF; // probability density function defining the life time of grass-pixels
        private float mGrassLifThreshold; // if LIF>threshold, then the grass is considered as occupatied by grass
        //private readonly GrassCoverLayers mLayers; // visualization

        // access
        /// returns 'true' if the module is enabled
        public bool IsEnabled { get; private set; }
        ///
        public double Effect(Int16 level) { return mEffect[level]; }
        public double Cover(Int16 data) { return mType == GrassAlgorithmType.Pixel ? data : data / (double)(Steps - 1); }

        /// retrieve the grid of current grass cover
        public Grid<Int16> Grid { get; private set; }

        public GrassCover()
        {
            this.mEffect = new double[GrassCover.Steps];
            this.mGrassEffect = new Expression();
            this.mGrassPotential = new Expression();
            //this.mLayers = new GrassCoverLayers();
            this.mPDF = new RandomCustomPdf();
            this.mType = GrassAlgorithmType.Invalid;

            this.Grid = new Grid<short>();
            this.IsEnabled = false;

            //this.mLayers.SetGrid(this.Grid);
        }

        // TODO: should be connected to seedling establishment
        //public double GetRegenerationInhibition(Point lightCellIndex)
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
            if (projectFile.Model.Settings.Grass.Enabled == false)
            {
                // clear grid
                this.Grid.Clear();
                this.IsEnabled = false;
                Debug.WriteLine("grass module not enabled");
                return;
            }
            // create the grid
            this.Grid.Setup(landscape.LightGrid.PhysicalExtent, landscape.LightGrid.CellSize);
            this.Grid.FillDefault();
            // mask out out-of-project areas
            Grid<HeightCell> heightGrid = landscape.HeightGrid;
            for (int index = 0; index < this.Grid.Count; ++index)
            {
                if (heightGrid[this.Grid.Index5(index)].IsOnLandscape() == false)
                {
                    this.Grid[index] = -1;
                }
            }

            this.mType = projectFile.Model.Settings.Grass.Type;
            if (this.mType == GrassAlgorithmType.Pixel)
            {
                // setup of pixel based / discrete approach
                string? formula = projectFile.Model.Settings.Grass.GrassDuration;
                if (String.IsNullOrEmpty(formula))
                {
                    throw new NotSupportedException("setup(): missing equation for 'grassDuration'.");
                }
                this.mPDF.Setup(formula, 0.0, 100.0);
                //mGrassEffect.setExpression(formula);

                this.mGrassLifThreshold = projectFile.Model.Settings.Grass.LifThreshold;

                // clear array
                for (int stepIndex = 0; stepIndex < Steps; ++stepIndex)
                {
                    this.mEffect[stepIndex] = 0.0;
                }
            }
            else
            {
                // setup of continuous grass concept
                string? grassPotential = projectFile.Model.Settings.Grass.GrassPotential;
                if (String.IsNullOrEmpty(grassPotential))
                {
                    throw new NotSupportedException("Required expression 'grassPotential' is missing.");
                }
                this.mGrassPotential.SetExpression(grassPotential);
                this.mGrassPotential.Linearize(0.0, 1.0, Math.Min(GrassCover.Steps, 1000));

                string? grassEffect = projectFile.Model.Settings.Grass.GrassEffect;
                if (String.IsNullOrEmpty(grassEffect))
                {
                    throw new NotSupportedException("Required expression 'grassEffect' is missing.");
                }
                this.mGrassEffect.SetExpression(grassEffect);

                this.mMaxTimeLag = projectFile.Model.Settings.Grass.MaxTimeLag;
                if (this.mMaxTimeLag == 0)
                {
                    throw new NotSupportedException("Value of 'maxTimeLag' is invalid or missing.");
                }
                this.mGrowthRate = GrassCover.Steps / this.mMaxTimeLag;

                // set up the effect on regeneration in NSTEPS steps
                for (int stepIndex = 0; stepIndex < GrassCover.Steps; ++stepIndex)
                {
                    double effect = mGrassEffect.Evaluate(stepIndex / (double)(GrassCover.Steps - 1));
                    this.mEffect[stepIndex] = Maths.Limit(effect, 0.0, 1.0);
                }

                this.mMaxState = (Int16)(Maths.Limit(mGrassPotential.Evaluate(1.0F), 0.0, 1.0) * (Steps - 1)); // the max value of the potential function
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
            if (mType == GrassAlgorithmType.Continuous)
            {
                Int16 grassValue = (Int16)(0.01 * percentGrassCover * (GrassCover.Steps - 1));
                if (grassValue > mMaxState)
                {
                    grassValue = mMaxState;
                }
                for (int lightCell = 0; lightCell < lightCells.Count; ++lightCell)
                {
                    this.Grid[lightCells[lightCell].Key] = grassValue;
                }
            }
            else
            {
                for (int lightCell = 0; lightCell < lightCells.Count; ++lightCell)
                {
                    if (percentGrassCover > randomGenerator.GetRandomInteger(0, 100))
                    {
                        this.Grid[lightCells[lightCell].Key] = (Int16)mPDF.GetRandomValue(randomGenerator);
                    }
                    else
                    {
                        this.Grid[lightCells[lightCell].Key] = 0;
                    }
                }
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
            if (mType == GrassAlgorithmType.Continuous)
            {
                // loop over every LIF pixel
                //int cellsSkipped = 0;
                for (int cellIndex = 0; cellIndex != lightGrid.Count; ++cellIndex)
                {
                    // calculate potential grass vegetation cover
                    if (lightGrid[cellIndex] == 1.0F && this.Grid[cellIndex] == mMaxState)
                    {
                        //++cellsSkipped;
                        continue;
                    }

                    int potential = (int)(Maths.Limit(mGrassPotential.Evaluate(lightGrid[cellIndex]), 0.0, 1.0) * (Steps - 1));
                    this.Grid[cellIndex] = (Int16)Math.Min(this.Grid[cellIndex] + mGrowthRate, potential);

                }
                //Debug.WriteLine("skipped" << skipped;
            }
            else
            {
                // type = Pixel
                for (int cellIndex = 0; cellIndex < lightGrid.Count; ++cellIndex)
                {
                    if (this.Grid[cellIndex] < 0)
                    {
                        continue;
                    }
                    if (this.Grid[cellIndex] > 1)
                    {
                        this.Grid[cellIndex]--; // count down the years (until gr=1)
                    }

                    if (this.Grid[cellIndex] == 0 && lightGrid[cellIndex] > mGrassLifThreshold)
                    {
                        // enable grass cover
                        this.Grid[cellIndex] = (Int16)(Math.Max(mPDF.GetRandomValue(randomGenerator), 0.0) + 1); // switch on...
                    }
                    if (this.Grid[cellIndex] == 1 && lightGrid[cellIndex] < mGrassLifThreshold)
                    {
                        // now LIF is below the threshold - this enables the pixel get grassy again
                        this.Grid[cellIndex] = 0;
                    }
                }
            }
        }
    }
}
