using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Core
{
    public class GrassCover
    {
        private const int GRASSCOVERSTEPS = 32000;

        private GrassAlgorithmType mType;
        private readonly Expression mGrassPotential; ///< function defining max. grass cover [0..1] as function of the LIF pixel value
        private readonly Expression mGrassEffect; ///< equation giving probability of *prohibiting* regeneration as a function of grass level [0..1]
        private int mMaxTimeLag; ///< maximum duration (years) from 0 to full cover
        private readonly double[] mEffect; ///< effect lookup table
        private int mGrowthRate; ///< max. annual growth rate of herbs and grasses (in 1/256th)
        private Int16 mMaxState; ///< potential at lif=1

        private readonly RandomCustomPdf mPDF; ///< probability density function defining the life time of grass-pixels
        private float mGrassLIFThreshold; ///< if LIF>threshold, then the grass is considered as occupatied by grass
        private readonly GrassCoverLayers mLayers; // visualization

        // access
        /// returns 'true' if the module is enabled
        public bool IsEnabled { get; private set; }
        ///
        public double Effect(Int16 level) { return mEffect[level]; }
        public double Cover(Int16 data) { return mType == GrassAlgorithmType.Pixel ? data : data / (double)(GRASSCOVERSTEPS - 1); }

        /// retrieve the grid of current grass cover
        public Grid<Int16> Grid { get; private set; }

        public GrassCover()
        {
            this.mEffect = new double[GrassCover.GRASSCOVERSTEPS];
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

        public void Setup()
        {
            XmlHelper xml = GlobalSettings.Instance.Settings;
            if (!xml.GetBool("model.settings.grass.enabled"))
            {
                // clear grid
                Grid.Clear();
                IsEnabled = false;
                Debug.WriteLine("grass module not enabled");
                return;
            }
            // create the grid
            Grid.Setup(GlobalSettings.Instance.Model.LightGrid.PhysicalExtent, GlobalSettings.Instance.Model.LightGrid.CellSize);
            Grid.ClearDefault();
            // mask out out-of-project areas
            Grid<HeightGridValue> hg = GlobalSettings.Instance.Model.HeightGrid;
            for (int i = 0; i < Grid.Count; ++i)
            {
                if (!hg[Grid.Index5(i)].IsValid())
                {
                    Grid[i] = -1;
                }
            }

            mType = Enum.Parse<GrassAlgorithmType>(xml.GetString("model.settings.grass.type"), ignoreCase: true);
            if (mType == GrassAlgorithmType.Pixel)
            {
                // setup of pixel based / discrete approach
                string formula = xml.GetString("model.settings.grass.grassDuration");
                if (String.IsNullOrEmpty(formula))
                {
                    throw new NotSupportedException("setup(): missing equation for 'grassDuration'.");
                }
                mPDF.Setup(formula, 0.0, 100.0);
                //mGrassEffect.setExpression(formula);

                mGrassLIFThreshold = (float)xml.GetDouble("model.settings.grass.LIFThreshold", 0.2);

                // clear array
                for (int i = 0; i < GRASSCOVERSTEPS; ++i)
                {
                    mEffect[i] = 0.0;
                }
            }
            else
            {
                // setup of continuous grass concept
                string formula = xml.GetString("model.settings.grass.grassPotential");
                if (String.IsNullOrEmpty(formula))
                {
                    throw new NotSupportedException("setup of 'grass': required expression 'grassPotential' is missing.");
                }
                mGrassPotential.SetExpression(formula);
                mGrassPotential.Linearize(0.0, 1.0, Math.Min(GRASSCOVERSTEPS, 1000));

                formula = xml.GetString("model.settings.grass.grassEffect");
                if (String.IsNullOrEmpty(formula))
                {
                    throw new NotSupportedException("setup of 'grass': required expression 'grassEffect' is missing.");
                }
                mGrassEffect.SetExpression(formula);
                mMaxTimeLag = (int)(xml.GetDouble("model.settings.grass.maxTimeLag"));
                if (mMaxTimeLag == 0)
                {
                    throw new NotSupportedException("setup of 'grass': value of 'maxTimeLag' is invalid or missing.");
                }
                mGrowthRate = GRASSCOVERSTEPS / mMaxTimeLag;

                // set up the effect on regeneration in NSTEPS steps
                for (int i = 0; i < GRASSCOVERSTEPS; ++i)
                {
                    double effect = mGrassEffect.Calculate(i / (double)(GRASSCOVERSTEPS - 1));
                    mEffect[i] = Global.Limit(effect, 0.0, 1.0);
                }

                mMaxState = (Int16)(Global.Limit(mGrassPotential.Calculate(1.0F), 0.0, 1.0) * (GRASSCOVERSTEPS - 1)); // the max value of the potential function
            }

            this.IsEnabled = true;
            // Debug.WriteLine("setup of grass cover complete.");
        }

        public void SetInitialValues(List<KeyValuePair<int, float>> LIFpixels, int percent)
        {
            if (!IsEnabled)
            {
                return;
            }
            if (mType == GrassAlgorithmType.Continuous)
            {
                Int16 cval = (Int16)(Global.Limit(percent / 100.0, 0.0, 1.0) * (GRASSCOVERSTEPS - 1));
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
                    if (percent > RandomGenerator.Random(0, 100))
                    {
                        Grid[LIFpixels[it].Key] = (Int16)mPDF.Get();
                    }
                    else
                    {
                        Grid[LIFpixels[it].Key] = 0;
                    }
                }
            }
        }

        public void Execute()
        {
            if (!IsEnabled)
            {
                return;
            }
            using DebugTimer t = new DebugTimer("GrassCover.Execute()");

            // Main function of the grass submodule
            Grid<float> lifGrid = GlobalSettings.Instance.Model.LightGrid;
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

                    int potential = (int)(Global.Limit(mGrassPotential.Calculate(lifGrid[lif]), 0.0, 1.0) * (GRASSCOVERSTEPS - 1));
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
                        Grid[gr] = (Int16)(Math.Max(mPDF.Get(), 0.0) + 1); // switch on...
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
