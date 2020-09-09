using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.core
{
    internal class GrassCover
    {
        private const int GRASSCOVERSTEPS = 32000;

        private GrassAlgorithmType mType;
        private bool mEnabled; ///< is module enabled?
        private Expression mGrassPotential; ///< function defining max. grass cover [0..1] as function of the LIF pixel value
        private Expression mGrassEffect; ///< equation giving probability of *prohibiting* regeneration as a function of grass level [0..1]
        private int mMaxTimeLag; ///< maximum duration (years) from 0 to full cover
        private double[] mEffect; ///< effect lookup table
        private Grid<Int16> mGrid; ///< grid covering state of grass cover (in integer steps)
        private int mGrowthRate; ///< max. annual growth rate of herbs and grasses (in 1/256th)
        private Int16 mMaxState; ///< potential at lif=1

        private RandomCustomPDF mPDF; ///< probability density function defining the life time of grass-pixels
        private float mGrassLIFThreshold; ///< if LIF>threshold, then the grass is considered as occupatied by grass
        private GrassCoverLayers mLayers; // visualization

        // access
        /// returns 'true' if the module is enabled
        public bool enabled() { return mEnabled; }
        ///
        public double effect(Int16 level) { return mEffect[level]; }
        public double cover(Int16 data) { return mType == GrassAlgorithmType.Pixel ? data : data / (double)(GRASSCOVERSTEPS - 1); }


        /// main function
        public double regenerationInhibition(Point lif_index)
        {

            if (mType == GrassAlgorithmType.Pixel)
            // -1: off, out of project area, 0: off, ready to get grassy again, 1: off (waiting for LIF threshold), >1 on, counting down
            {
                return mGrid.constValueAtIndex(lif_index) > 1 ? 1.0 : 0.0;
            }
            // type continuous
            return mEnabled ? effect(mGrid.constValueAtIndex(lif_index)) : 0.0;
        }

        /// retrieve the grid of current grass cover
        public Grid<Int16> grid() { return mGrid; }

        public GrassCover()
        {
            mEffect = new double[GRASSCOVERSTEPS];
            mLayers = new GrassCoverLayers();
            mLayers.setGrid(mGrid, this);
            mEnabled = false;
            mType = GrassAlgorithmType.Invalid;
        }

        public void setup()
        {
            XmlHelper xml = GlobalSettings.instance().settings();
            if (!xml.valueBool("model.settings.grass.enabled"))
            {
                // clear grid
                mGrid.clear();
                GlobalSettings.instance().controller().removeLayers(mLayers);
                mEnabled = false;
                Debug.WriteLine("grass module not enabled");
                return;
            }
            // create the grid
            mGrid.setup(GlobalSettings.instance().model().grid().metricRect(), GlobalSettings.instance().model().grid().cellsize());
            mGrid.wipe();
            // mask out out-of-project areas
            Grid<HeightGridValue> hg = GlobalSettings.instance().model().heightGrid();
            for (int i = 0; i < mGrid.count(); ++i)
            {
                if (!hg.valueAtIndex(mGrid.index5(i)).isValid())
                {
                    mGrid[i] = -1;
                }
            }

            mType = Enum.Parse<GrassAlgorithmType>(xml.value("model.settings.grass.type"));
            if (mType == GrassAlgorithmType.Pixel)
            {
                // setup of pixel based / discrete approach
                string formula = xml.value("model.settings.grass.grassDuration");
                if (String.IsNullOrEmpty(formula))
                {
                    throw new NotSupportedException("setup(): missing equation for 'grassDuration'.");
                }
                mPDF.setup(formula, 0.0, 100.0);
                //mGrassEffect.setExpression(formula);

                mGrassLIFThreshold = (float)xml.valueDouble("model.settings.grass.LIFThreshold", 0.2);

                // clear array
                for (int i = 0; i < GRASSCOVERSTEPS; ++i)
                {
                    mEffect[i] = 0.0;
                }
            }
            else
            {
                // setup of continuous grass concept
                string formula = xml.value("model.settings.grass.grassPotential");
                if (String.IsNullOrEmpty(formula))
                {
                    throw new NotSupportedException("setup of 'grass': required expression 'grassPotential' is missing.");
                }
                mGrassPotential.setExpression(formula);
                mGrassPotential.linearize(0.0, 1.0, Math.Min(GRASSCOVERSTEPS, 1000));

                formula = xml.value("model.settings.grass.grassEffect");
                if (String.IsNullOrEmpty(formula))
                {
                    throw new NotSupportedException("setup of 'grass': required expression 'grassEffect' is missing.");
                }
                mGrassEffect.setExpression(formula);
                mMaxTimeLag = (int)(xml.valueDouble("model.settings.grass.maxTimeLag"));
                if (mMaxTimeLag == 0)
                {
                    throw new NotSupportedException("setup of 'grass': value of 'maxTimeLag' is invalid or missing.");
                }
                mGrowthRate = GRASSCOVERSTEPS / mMaxTimeLag;

                // set up the effect on regeneration in NSTEPS steps
                for (int i = 0; i < GRASSCOVERSTEPS; ++i)
                {
                    double effect = mGrassEffect.calculate(i / (double)(GRASSCOVERSTEPS - 1));
                    mEffect[i] = Global.limit(effect, 0.0, 1.0);
                }

                mMaxState = (Int16)(Global.limit(mGrassPotential.calculate(1.0F), 0.0, 1.0) * (GRASSCOVERSTEPS - 1)); // the max value of the potential function
            }

            GlobalSettings.instance().controller().addLayers(mLayers, "grass cover");
            mEnabled = true;
            Debug.WriteLine("setup of grass cover complete.");
        }

        public void setInitialValues(List<KeyValuePair<int, float>> LIFpixels, int percent)
        {
            if (!enabled())
            {
                return;
            }
            if (mType == GrassAlgorithmType.Continuous)
            {
                Int16 cval = (Int16)(Global.limit(percent / 100.0, 0.0, 1.0) * (GRASSCOVERSTEPS - 1));
                if (cval > mMaxState)
                {
                    cval = mMaxState;
                }
                Grid<float> lif_grid = GlobalSettings.instance().model().grid();
                for (int it = 0; it < LIFpixels.Count; ++it)
                {
                    mGrid[LIFpixels[it].Key] = cval;
                }
            }
            else
            {
                // mType == Pixel
                Grid<float> lif_grid = GlobalSettings.instance().model().grid();
                for (int it = 0; it < LIFpixels.Count; ++it)
                {
                    if (percent > RandomGenerator.irandom(0, 100))
                    {
                        mGrid[LIFpixels[it].Key] = (Int16)mPDF.get();
                    }
                    else
                    {
                        mGrid[LIFpixels[it].Key] = 0;
                    }
                }
            }
        }

        public void execute()
        {
            if (!enabled())
            {
                return;
            }
            using DebugTimer t = new DebugTimer("GrassCover");

            // Main function of the grass submodule
            Grid<float> lifGrid = GlobalSettings.instance().model().grid();
            int gr = 0;
            if (mType == GrassAlgorithmType.Continuous)
            {
                // loop over every LIF pixel
                int skipped = 0;
                for (int lif = 0; lif != lifGrid.count(); ++lif, ++gr)
                {
                    // calculate potential grass vegetation cover
                    if (lifGrid[lif] == 1.0F && mGrid[gr] == mMaxState)
                    {
                        ++skipped;
                        continue;
                    }

                    int potential = (int)(Global.limit(mGrassPotential.calculate(lifGrid[lif]), 0.0, 1.0) * (GRASSCOVERSTEPS - 1));
                    mGrid[gr] = (Int16)(Math.Min(mGrid[gr] + mGrowthRate, potential));

                }
                //Debug.WriteLine("skipped" << skipped;
            }
            else
            {
                // type = Pixel
                for (int lif = 0; lif < lifGrid.count(); ++lif, ++gr)
                {
                    if (mGrid[gr] < 0)
                    {
                        continue;
                    }
                    if (mGrid[gr] > 1)
                    {
                        mGrid[gr]--; // count down the years (until gr=1)
                    }

                    if (mGrid[gr] == 0 && lifGrid[lif] > mGrassLIFThreshold)
                    {
                        // enable grass cover
                        mGrid[gr] = (Int16)(Math.Max(mPDF.get(), 0.0) + 1); // switch on...
                    }
                    if (mGrid[gr] == 1 && lifGrid[lif] < mGrassLIFThreshold)
                    {
                        // now LIF is below the threshold - this enables the pixel get grassy again
                        mGrid[gr] = 0;
                    }
                }
            }
        }
    }
}
