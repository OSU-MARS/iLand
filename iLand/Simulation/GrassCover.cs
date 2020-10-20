using iLand.Tools;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Simulation
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
        private float mGrassLIFThreshold; // if LIF>threshold, then the grass is considered as occupatied by grass
        private readonly GrassCoverLayers mLayers; // visualization

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
            this.mLayers = new GrassCoverLayers();
            this.mLayers.SetGrid(Grid);
            this.mPDF = new RandomCustomPdf();
            this.mType = GrassAlgorithmType.Invalid;

            this.Grid = new Grid<short>();
            this.IsEnabled = false;
        }

        /// main function
        public double RegenerationInhibition(Point lif_index)
        {
            if (mType == GrassAlgorithmType.Pixel)
            {
                // -1: off, out of project area, 0: off, ready to get grassy again, 1: off (waiting for LIF threshold), >1 on, counting down
                return Grid[lif_index] > 1 ? 1.0 : 0.0;
            }
            // type continuous
            return IsEnabled ? Effect(Grid[lif_index]) : 0.0;
        }

        public void Setup(Model model)
        {
            if (model.Project.Model.Settings.Grass.Enabled == false)
            {
                // clear grid
                Grid.Clear();
                IsEnabled = false;
                Debug.WriteLine("grass module not enabled");
                return;
            }
            // create the grid
            Grid.Setup(model.LightGrid.PhysicalExtent, model.LightGrid.CellSize);
            Grid.ClearDefault();
            // mask out out-of-project areas
            Grid<HeightCell> heightGrid = model.HeightGrid;
            for (int i = 0; i < Grid.Count; ++i)
            {
                if (!heightGrid[Grid.Index5(i)].IsInWorld())
                {
                    Grid[i] = -1;
                }
            }

            mType = model.Project.Model.Settings.Grass.Type;
            if (mType == GrassAlgorithmType.Pixel)
            {
                // setup of pixel based / discrete approach
                string formula = model.Project.Model.Settings.Grass.GrassDuration;
                if (String.IsNullOrEmpty(formula))
                {
                    throw new NotSupportedException("setup(): missing equation for 'grassDuration'.");
                }
                mPDF.Setup(model, formula, 0.0, 100.0);
                //mGrassEffect.setExpression(formula);
                
                mGrassLIFThreshold = model.Project.Model.Settings.Grass.LifThreshold;

                // clear array
                for (int stepIndex = 0; stepIndex < Steps; ++stepIndex)
                {
                    mEffect[stepIndex] = 0.0;
                }
            }
            else
            {
                // setup of continuous grass concept
                string grassPotential = model.Project.Model.Settings.Grass.GrassPotential;
                if (String.IsNullOrEmpty(grassPotential))
                {
                    throw new NotSupportedException("Required expression 'grassPotential' is missing.");
                }
                mGrassPotential.SetExpression(grassPotential);
                mGrassPotential.Linearize(model, 0.0, 1.0, Math.Min(Steps, 1000));

                string grassEffect = model.Project.Model.Settings.Grass.GrassEffect;
                if (String.IsNullOrEmpty(grassEffect))
                {
                    throw new NotSupportedException("Required expression 'grassEffect' is missing.");
                }
                mGrassEffect.SetExpression(grassEffect);

                mMaxTimeLag = model.Project.Model.Settings.Grass.MaxTimeLag;
                if (mMaxTimeLag == 0)
                {
                    throw new NotSupportedException("Value of 'maxTimeLag' is invalid or missing.");
                }
                mGrowthRate = Steps / mMaxTimeLag;

                // set up the effect on regeneration in NSTEPS steps
                for (int stepIndex = 0; stepIndex < Steps; ++stepIndex)
                {
                    double effect = mGrassEffect.Evaluate(model, stepIndex / (double)(Steps - 1));
                    mEffect[stepIndex] = Global.Limit(effect, 0.0, 1.0);
                }

                mMaxState = (Int16)(Global.Limit(mGrassPotential.Evaluate(model, 1.0F), 0.0, 1.0) * (Steps - 1)); // the max value of the potential function
            }

            this.IsEnabled = true;
            // Debug.WriteLine("setup of grass cover complete.");
        }

        public void SetInitialValues(Model model, List<KeyValuePair<int, float>> LIFpixels, int percent)
        {
            if (!IsEnabled)
            {
                return;
            }
            if (mType == GrassAlgorithmType.Continuous)
            {
                Int16 cval = (Int16)(Global.Limit(percent / 100.0, 0.0, 1.0) * (Steps - 1));
                if (cval > mMaxState)
                {
                    cval = mMaxState;
                }
                for (int it = 0; it < LIFpixels.Count; ++it)
                {
                    Grid[LIFpixels[it].Key] = cval;
                }
            }
            else
            {
                for (int it = 0; it < LIFpixels.Count; ++it)
                {
                    if (percent > model.RandomGenerator.Random(0, 100))
                    {
                        Grid[LIFpixels[it].Key] = (Int16)mPDF.Get(model);
                    }
                    else
                    {
                        Grid[LIFpixels[it].Key] = 0;
                    }
                }
            }
        }

        public void Execute(Model model)
        {
            if (!IsEnabled)
            {
                return;
            }
            //using DebugTimer t = model.DebugTimers.Create("GrassCover.Execute()");

            // Main function of the grass submodule
            Grid<float> lifGrid = model.LightGrid;
            int gr = 0;
            if (mType == GrassAlgorithmType.Continuous)
            {
                // loop over every LIF pixel
                int skipped = 0;
                for (int lif = 0; lif != lifGrid.Count; ++lif, ++gr)
                {
                    // calculate potential grass vegetation cover
                    if (lifGrid[lif] == 1.0F && Grid[gr] == mMaxState)
                    {
                        ++skipped;
                        continue;
                    }

                    int potential = (int)(Global.Limit(mGrassPotential.Evaluate(model, lifGrid[lif]), 0.0, 1.0) * (Steps - 1));
                    Grid[gr] = (Int16)(Math.Min(Grid[gr] + mGrowthRate, potential));

                }
                //Debug.WriteLine("skipped" << skipped;
            }
            else
            {
                // type = Pixel
                for (int lif = 0; lif < lifGrid.Count; ++lif, ++gr)
                {
                    if (Grid[gr] < 0)
                    {
                        continue;
                    }
                    if (Grid[gr] > 1)
                    {
                        Grid[gr]--; // count down the years (until gr=1)
                    }

                    if (Grid[gr] == 0 && lifGrid[lif] > mGrassLIFThreshold)
                    {
                        // enable grass cover
                        Grid[gr] = (Int16)(Math.Max(mPDF.Get(model), 0.0) + 1); // switch on...
                    }
                    if (Grid[gr] == 1 && lifGrid[lif] < mGrassLIFThreshold)
                    {
                        // now LIF is below the threshold - this enables the pixel get grassy again
                        Grid[gr] = 0;
                    }
                }
            }
        }
    }
}
