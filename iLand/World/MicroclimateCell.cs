// C++/core/{ microclimate.h, microclimate.cpp }
using System;

namespace iLand.World
{
    public class MicroclimateCell
    {
        // use 16 bit per value
        // TODO: increase number of bits used in fixed <. floating point conversions
        //private UInt16 mEvergreenShare;
        private UInt16 scaledLeafAreaIndex;
        private UInt16 scaledShadeToleranceMean;
        private Int16 scaledTopographicPositionIndex;
        private Int16 scaledNorthnessCosine; /// northness (= cos(aspect) ) [-1 .. 1]
        //private UInt16 mSlope;

        public MicroclimateCell()
        {
            this.Clear();
        }

        public MicroclimateCell(float lai, float shade_tol, float tpi, float northness)
        {
            SetLeafAreaIndex(lai);
            SetShadeToleranceMean(shade_tol);
            SetTopographicPositionIndex(tpi);
            SetNorthness(northness);
        }

        public void Clear() // C++: MicroclimateCell::clear()
        {
            this.scaledLeafAreaIndex = 0;
            this.scaledTopographicPositionIndex = 0;
            this.scaledNorthnessCosine = 0;
        }

        public bool IsValid() // C++: MicroclimateCell::valid()
        {
            return this.scaledNorthnessCosine != Int16.MinValue;
        }

        public void Invalidate() // C++: MicroclimateCell::setInvalid()
        {
            this.scaledNorthnessCosine = Int16.MinValue;
        }

        /// set conifer share on the cell (0..1)
        //public void setEvergreenShare(float share) { mEvergreenShare = static_cast<short unsigned int>(share * 1000.); /* save as short int */ }
        /// conifer share from 0 (=0%) to 1 (=100%). Empty cells have a share of 0.
        //public float evergreenShare() const { return static_cast<float>(mEvergreenShare) / 1000.; }

        /// set conifer share on the cell (0..1)
        public void SetLeafAreaIndex(float lai)
        {
            this.scaledLeafAreaIndex = (UInt16)(1000.0F * lai); /* save as short int */
        }

        /// conifer share from 0 (=0%) to 1 (=100%). Empty cells have a share of 0.
        public float GetLeafAreaIndex() // C++: MicroclimateCell::LAI()
        {
            return scaledLeafAreaIndex / 1000.0F;
        }

        public void SetShadeToleranceMean(float shadeTolerance) // C++: MicroclimateCell::setShadeToleranceMean()
        {
            this.scaledShadeToleranceMean = (UInt16)(10000.0F * shadeTolerance); /*stol: 1-5*/
        }

        /// basal area weighted shade tolerance class (iLand species parameter)
        public float GetShadeToleranceMean() // C++: MicroclimateCell::shadeToleranceMean()
        {
            return this.scaledShadeToleranceMean / 10000.0F;
        }

        public float GetNorthness() // C++: MicroclimateCell::northness()
        {
            return this.scaledNorthnessCosine > Int16.MinValue ? (float)scaledNorthnessCosine / 10000.0F : 0.0F;
        }

        public void SetNorthness(float value) // C++: MicroclimateCell::setNorthness()
        {
            this.scaledNorthnessCosine = (Int16)(value * 10000);
        }

        /// slope in (abs) degrees (0..90)
        //float slope() const {  return  static_cast<float>(mSlope) / 500. ; }
        //void setSlope(float value)  { mSlope = static_cast<short int>(value * 500); }

        /// topographic Position Index (~ difference between elevation and average elevation in with a radius)
        public float GetTopographicPositionIndex() // C++: MicroclimateCell::topographicPositionIndex()
        {
            return this.scaledTopographicPositionIndex / 10.0F;
        }

        public void SetTopographicPositionIndex(float value) // C++: MicroclimateCell::setTopographicPositionIndex()
        {
            this.scaledTopographicPositionIndex = (Int16)(10.0F * value);
        }

        /// minimum microclimate buffering
        /// for a given resource unit and month (0..1)
        public float GetMinimumMicroclimateBuffering(ResourceUnit resourceUnit, int month) // C++: MicroclimateCell::minimumMicroclimateBuffering()
        {
            float mean_temp = resourceUnit.Weather.TimeSeries.GetMonthlyMeanDailyMinimumTemperature(month);
            return this.GetMinimumMicroclimateBuffering(mean_temp);
        }

        /// maximum microclimate buffering
        /// for a given resource unit and month (0..1)
        public float GetMaximumMicroclimateBuffering(ResourceUnit resourceUnit, int month) // C++: MicroclimateCell::maximumMicroclimateBuffering()
        {
            float mean_temp = resourceUnit.Weather.TimeSeries.GetMonthlyMeanDailyMaximumTemperature(month);
            return this.GetMaximumMicroclimateBuffering(mean_temp);
        }

        /// faster calculation minimum microclimate buffering, when growingseasonindex is known
        public float GetMinimumMicroclimateBuffering(float macro_t_min) // C++: MicroclimateCell::minimumMicroclimateBuffering()
        {
            // old: "Minimum temperature buffer ~ -1.7157325 - 0.0187969*North + 0.0161997*RelEmin500 + 0.0890564*lai + 0.3414672*stol + 0.8302521*GSI + 0.0208083*prop_evergreen - 0.0107308*GSI:prop_evergreen"
            // Buffer_minT = 0.6077 – 0.0088 * Macroclimate_minT + 0.3548 * Northness  + 0.0872 * Slope + 0.0202 * TPI - 0.0330 * LAI + 0.0502 * STol – 0.7601 * Evergreen – 0.8385 * GSI:Evergreen
            // version nov 2023: Tminbuffer = 1.4570 - 0.0248 × Tminmacroclimate + 0.2627 × Northness + 0.0158 × TPI + 0.0227 × LAI - 0.2031 × STol
            float buf = 1.4570F - 0.0248F * macro_t_min +
                                  0.2627F * GetNorthness() +
                                  0.0158F * GetTopographicPositionIndex() +
                                  0.0227F * GetLeafAreaIndex() +
                                  -0.2031F * GetShadeToleranceMean();
            if (Single.Abs(buf) > 10.0F)
            {
                buf = 0.0F;
            }
            return buf;
        }

        public float GetMaximumMicroclimateBuffering(float macro_t_max) // C++: MicroclimateCell::maximumMicroclimateBuffering()
        {
            // old: "Maximum temperature buffer ~ 1.9058391 - 0.2528409*North - 0.0027037*RelEmin500 - 0.1549061*lai - 0.3806543*stol - 1.2863341*GSI - 0.8070951*prop_evergreen + 0.5004421*GSI:prop_evergreen"
            // Buffer_maxT = 2.7839 – 0.2729 * Macroclimate_maxT - 0.5403 * Northness  - 0.1127 * Slope + 0.0155 * TPI – 0.3182 * LAI + 0.1403 * STol – 1.1039 * Evergreen + 6.9670 * GSI:Evergreen
            // version nov 23: Tmaxbuffer = 0.9767 - 0.1932 × Tmaxmacroclimate - 0.5729 × Northness + 0.0140 × TPI - 0.3948 × LAI + 0.4419 × STol
            float buf = 0.9767F - 0.1932F * macro_t_max +
                                -0.5729F * GetNorthness() +
                                0.0140F * GetTopographicPositionIndex() +
                                -0.3948F * GetLeafAreaIndex() +
                                0.4419F * GetShadeToleranceMean();
            if (Single.Abs(buf) > 10.0F)
            {
                buf = 0.0F;
            }
            return buf;
        }
    }
}
