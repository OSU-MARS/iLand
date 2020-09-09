using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.core
{
    /** @class WaterCycle
        @ingroup core
        simulates the water cycle on a ResourceUnit.
        The WaterCycle is simulated with a daily time step on the spatial level of a ResourceUnit. Related are
        the snow module (SnowPack), and Canopy module that simulates the interception (and evaporation) of precipitation and the
        transpiration from the canopy.
        The WaterCycle covers the "soil water bucket". Main entry function is run().

        See http://iland.boku.ac.at/water+cycle
        */
    internal class WaterCycle
    {
        private int mLastYear; ///< last year of execution
        private double mPsi_koeff_b; ///< see psiFromHeight()
        private double mPsi_sat; ///< see psiFromHeight(), kPa
        private double mTheta_sat; ///< see psiFromHeight(), [-], m3/m3
        private ResourceUnit mRU; ///< resource unit to which this watercycle is connected
        private Canopy mCanopy; ///< object representing the forest canopy (interception, evaporation)
        private SnowPack mSnowPack; ///< object representing the snow cover (aggregation, melting)
        private double mSoilDepth; ///< depth of the soil (without rocks) in mm
        private double mContent; ///< current water content in mm water column of the soil.
        private double mFieldCapacity; ///< bucket height of field-capacity (eq. -15kPa) (mm)
        private double mPermanentWiltingPoint; ///< bucket "height" of PWP (is fixed to -4MPa) (mm)
        private double[] mPsi; ///< soil water potential for each day in kPa
        private double mLAINeedle;
        private double mLAIBroadleaved;
        private double mCanopyConductance; ///< m/s
        // annual sums
        public double mTotalET; ///< annual sum of evapotranspiration (mm)
        public double mTotalExcess; ///< annual sum of water loss due to lateral outflow/groundwater flow (mm)
        public double mSnowRad; ///< sum of radiation input (MJ/m2) for days with snow cover (used in albedo calculations)
        public double mSnowDays; ///< # of days with snowcover >0

        public void setContent(double content, double snow_mm) { mContent = content; mSnowPack.setSnow(snow_mm); }
        public double fieldCapacity() { return mFieldCapacity; } ///< field capacity (mm)
        public double psi_kPa(int doy) { return mPsi[doy]; } ///< soil water potential for the day 'doy' (0-index) in kPa
        public double soilDepth() { return mSoilDepth; } ///< soil depth in mm
        public double currentContent() { return mContent; } ///< current water content in mm
        public double currentSnowPack() { return mSnowPack.snowPack(); } ///< current water stored as snow (mm water)
        public double canopyConductance() { return mCanopyConductance; } ///< current canopy conductance (LAI weighted CC of available tree species) (m/s)
                                                                         /// monthly values for PET (mm sum)
        public double[] referenceEvapotranspiration() { return mCanopy.referenceEvapotranspiration(); }

        public WaterCycle()
        {
            mPsi = new double[366];
            mSoilDepth = 0;
            mLastYear = -1;
        }

        public void setup(ResourceUnit ru)
        {
            mRU = ru;
            // get values...
            mFieldCapacity = 0.0; // on top
            XmlHelper xml = GlobalSettings.instance().settings();
            mSoilDepth = xml.valueDouble("model.site.soilDepth", 0.0) * 10; // convert from cm to mm
            double pct_sand = xml.valueDouble("model.site.pctSand");
            double pct_silt = xml.valueDouble("model.site.pctSilt");
            double pct_clay = xml.valueDouble("model.site.pctClay");
            if (Math.Abs(100.0 - (pct_sand + pct_silt + pct_clay)) > 0.01)
            {
                throw new NotSupportedException(String.Format("Setup Watercycle: soil composition percentages do not sum up to 100. Sand: {0}, Silt: {1} Clay: {2}", pct_sand, pct_silt, pct_clay));
            }

            // calculate soil characteristics based on empirical functions (Schwalm & Ek, 2004)
            // note: the variables are percentages [0..100]
            mPsi_sat = -Math.Exp((1.54 - 0.0095 * pct_sand + 0.0063 * pct_silt) * Math.Log(10)) * 0.000098; // Eq. 83
            mPsi_koeff_b = -(3.1 + 0.157 * pct_clay - 0.003 * pct_sand);  // Eq. 84
            mTheta_sat = 0.01 * (50.5 - 0.142 * pct_sand - 0.037 * pct_clay); // Eq. 78
            mCanopy.setup();

            mPermanentWiltingPoint = heightFromPsi(-4000); // maximum psi is set to a constant of -4MPa
            if (xml.valueBool("model.settings.waterUseSoilSaturation", false) == false)
            {
                mFieldCapacity = heightFromPsi(-15);
            }
            else
            {
                // =-EXP((1.54-0.0095* pctSand +0.0063* pctSilt)*LN(10))*0.000098
                double psi_sat = -Math.Exp((1.54 - 0.0095 * pct_sand + 0.0063 * pct_silt) * Math.Log(10.0)) * 0.000098;
                mFieldCapacity = heightFromPsi(psi_sat);
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("psi: saturation " + psi_sat + " field capacity: " + mFieldCapacity);
                }
            }

            mContent = mFieldCapacity; // start with full water content (in the middle of winter)
            if (GlobalSettings.instance().logLevelDebug())
            {
                Debug.WriteLine("setup of water: Psi_sat (kPa) " + mPsi_sat + " Theta_sat " + mTheta_sat + " coeff. b " + mPsi_koeff_b);
            }
            mCanopyConductance = 0.0;
            mLastYear = -1;

            // canopy settings
            mCanopy.mNeedleFactor = xml.valueDouble("model.settings.interceptionStorageNeedle", 4.0);
            mCanopy.mDecidousFactor = xml.valueDouble("model.settings.interceptionStorageBroadleaf", 2.0);
            mSnowPack.mSnowTemperature = xml.valueDouble("model.settings.snowMeltTemperature", 0.0);

            mTotalET = mTotalExcess = mSnowRad = 0.0;
            mSnowDays = 0;
        }

        /** function to calculate the water pressure [saugspannung] for a given amount of water.
            returns water potential in kPa.
          see http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance */
        public double psiFromHeight(double mm)
        {
            // psi_x = psi_ref * ( rho_x / rho_ref) ^ b
            if (mm < 0.001)
            {
                return -100000000.0;
            }
            double psi_x = mPsi_sat * Math.Pow((mm / mSoilDepth / mTheta_sat), mPsi_koeff_b);
            return psi_x; // pis
        }

        /// calculate the height of the water column for a given pressure
        /// return water amount in mm
        /// see http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance
        public double heightFromPsi(double psi_kpa)
        {
            // rho_x = rho_ref * (psi_x / psi_ref)^(1/b)
            double h = mSoilDepth * mTheta_sat * Math.Pow(psi_kpa / mPsi_sat, 1.0 / mPsi_koeff_b);
            return h;
        }

        /// get canopy characteristics of the resource unit.
        /// It is important, that species-statistics are valid when this function is called (LAI)!
        public void getStandValues()
        {
            mLAINeedle = mLAIBroadleaved = 0.0;
            mCanopyConductance = 0.0;
            double ground_vegetationCC = 0.02;
            foreach (ResourceUnitSpecies rus in mRU.ruSpecies()) 
            {
                double lai = rus.constStatistics().leafAreaIndex();
                if (rus.species().isConiferous())
                {
                    mLAINeedle += lai;
                }
                else
                {
                    mLAIBroadleaved += lai;
                }
                mCanopyConductance += rus.species().canopyConductance() * lai; // weigh with LAI
            }
            double total_lai = mLAIBroadleaved + mLAINeedle;

            // handle cases with LAI < 1 (use generic "ground cover characteristics" instead)
            /* The LAI used here is derived from the "stockable" area (and not the stocked area).
               If the stand has gaps, the available trees are "thinned" across the whole area. Otherwise (when stocked area is used)
               the LAI would overestimate the transpiring canopy. However, the current solution overestimates e.g. the interception.
               If the "thinned out" LAI is below one, the rest (i.e. the gaps) are thought to be covered by ground vegetation.
            */
            if (total_lai < 1.0)
            {
                mCanopyConductance += (ground_vegetationCC) * (1.0 - total_lai);
                total_lai = 1.0;
            }
            mCanopyConductance /= total_lai;

            if (total_lai < GlobalSettings.instance().model().settings().laiThresholdForClosedStands)
            {
                // following Landsberg and Waring: when LAI is < 3 (default for laiThresholdForClosedStands), a linear "ramp" from 0 to 3 is assumed
                // http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance
                mCanopyConductance *= total_lai / GlobalSettings.instance().model().settings().laiThresholdForClosedStands;
            }
            if (GlobalSettings.instance().logLevelInfo())
            {
                Debug.WriteLine("WaterCycle:getStandValues: LAI needle " + mLAINeedle + " LAI Broadl: " + mLAIBroadleaved + " weighted avg. Conductance (m/2): " + mCanopyConductance);
            }
        }

        /// calculate responses for ground vegetation, i.e. for "unstocked" areas.
        /// this duplicates calculations done in Species.
        /// @return Minimum of vpd and soilwater response for default
        public double calculateBaseSoilAtmosphereResponse(double psi_kpa, double vpd_kpa)
        {
            // constant parameters used for ground vegetation:
            double mPsiMin = 1.5; // MPa
            double mRespVpdExponent = -0.6;
            // see SpeciesResponse::soilAtmosphereResponses()
            double water_resp;
            // see Species::soilwaterResponse:
            double psi_mpa = psi_kpa / 1000.0; // convert to MPa
            water_resp = Global.limit(1.0 - psi_mpa / mPsiMin, 0.0, 1.0);
            // see species::vpdResponse

            double vpd_resp;
            vpd_resp = Math.Exp(mRespVpdExponent * vpd_kpa);
            return Math.Min(water_resp, vpd_resp);
        }

        /// calculate combined VPD and soilwaterresponse for all species
        /// on the RU. This is used for the calc. of the transpiration.
        public double calculateSoilAtmosphereResponse(double psi_kpa, double vpd_kpa)
        {
            double total_response = 0; // LAI weighted minimum response for all speices on the RU
            double total_lai_factor = 0.0;
            foreach (ResourceUnitSpecies rus in mRU.ruSpecies())
            {
                if (rus.LAIfactor() > 0.0)
                {
                    // retrieve the minimum of VPD / soil water response for that species
                    rus.speciesResponse().soilAtmosphereResponses(psi_kpa, vpd_kpa, out double min_response);
                    total_response += min_response * rus.LAIfactor();
                    total_lai_factor += rus.LAIfactor();
                }
            }

            if (total_lai_factor < 1.0)
            {
                // the LAI is below 1: the rest is considered as "ground vegetation"
                total_response += calculateBaseSoilAtmosphereResponse(psi_kpa, vpd_kpa) * (1.0 - total_lai_factor);
            }

            // add an aging factor to the total response (averageAging: leaf area weighted mean aging value):
            // conceptually: response = min(vpd_response, water_response)*aging
            if (total_lai_factor == 1.0)
            {
                total_response *= mRU.averageAging(); // no ground cover: use aging value for all LA
            }
            else if (total_lai_factor > 0.0 && mRU.averageAging() > 0.0)
            {
                total_response *= (1.0 - total_lai_factor) * 1.0 + (total_lai_factor * mRU.averageAging()); // between 0..1: a part of the LAI is "ground cover" (aging=1)
            }

#if DEBUG
            if (mRU.averageAging() > 1.0 || mRU.averageAging() < 0.0 || total_response < 0 || total_response > 1.0)
            {
                Debug.WriteLine("water cycle: average aging invalid. aging: " + mRU.averageAging() + " total response " + total_response + " total lai factor: " + total_lai_factor);
            }
#endif
            //DBG_IF(mRU.averageAging()>1. || mRU.averageAging()<0.,"water cycle", "average aging invalid!" );
            return total_response;
        }

        /// Main Water Cycle function. This function triggers all water related tasks for
        /// one simulation year.
        /// @sa http://iland.boku.ac.at/water+cycle
        public void run()
        {
            // necessary?
            if (GlobalSettings.instance().currentYear() == mLastYear)
            {
                return;
            }
            using DebugTimer tw = new DebugTimer("water:run");
            WaterCycleData add_data = new WaterCycleData();

            // preparations (once a year)
            getStandValues(); // fetch canopy characteristics from iLand (including weighted average for mCanopyConductance)
            mCanopy.setStandParameters(mLAINeedle, mLAIBroadleaved, mCanopyConductance);

            // main loop over all days of the year
            double prec_mm, prec_after_interception, prec_to_soil, et, excess;
            Climate climate = mRU.climate();
            
            int doy = 0;
            mTotalExcess = 0.0;
            mTotalET = 0.0;
            mSnowRad = 0.0;
            mSnowDays = 0;
            for (int index = climate.begin(); index < climate.end(); ++index, ++doy)
            {
                ClimateDay day = climate[index];
                // (1) precipitation of the day
                prec_mm = day.preciptitation;
                // (2) interception by the crown
                prec_after_interception = mCanopy.flow(prec_mm);
                // (3) storage in the snow pack
                prec_to_soil = mSnowPack.flow(prec_after_interception, day.temperature);
                // save extra data (used by e.g. fire module)
                add_data.water_to_ground[doy] = prec_to_soil;
                add_data.snow_cover[doy] = mSnowPack.snowPack();
                if (mSnowPack.snowPack() > 0.0)
                {
                    mSnowRad += day.radiation;
                    mSnowDays++;
                }

                // (4) add rest to soil
                mContent += prec_to_soil;

                excess = 0.0;
                if (mContent > mFieldCapacity)
                {
                    // excess water runoff
                    excess = mContent - mFieldCapacity;
                    mTotalExcess += excess;
                    mContent = mFieldCapacity;
                }

                double current_psi = psiFromHeight(mContent);
                mPsi[doy] = current_psi;

                // (5) transpiration of the vegetation (and of water intercepted in canopy)
                // calculate the LAI-weighted response values for soil water and vpd:
                double interception_before_transpiration = mCanopy.interception();
                double combined_response = calculateSoilAtmosphereResponse(current_psi, day.vpd);
                et = mCanopy.evapotranspiration3PG(day, climate.daylength_h(doy), combined_response);
                // if there is some flow from intercepted water to the ground -> add to "water_to_the_ground"
                if (mCanopy.interception() < interception_before_transpiration)
                {
                    add_data.water_to_ground[doy] += interception_before_transpiration - mCanopy.interception();
                }

                mContent -= et; // reduce content (transpiration)
                                // add intercepted water (that is *not* evaporated) again to the soil (or add to snow if temp too low -> call to snowpack)
                mContent += mSnowPack.add(mCanopy.interception(), day.temperature);

                // do not remove water below the PWP (fixed value)
                if (mContent < mPermanentWiltingPoint)
                {
                    et -= mPermanentWiltingPoint - mContent; // reduce et (for bookkeeping)
                    mContent = mPermanentWiltingPoint;
                }

                mTotalET += et;

                //DBGMODE(
                if (GlobalSettings.instance().isDebugEnabled(DebugOutputs.dWaterCycle))
                {
                    List<object> output = GlobalSettings.instance().debugList(day.id(), DebugOutputs.dWaterCycle);
                    // climatic variables
                    output.AddRange(new object[] { day.id(), mRU.index(), mRU.id(), day.temperature, day.vpd, day.preciptitation, day.radiation });
                    output.Add(combined_response); // combined response of all species on RU (min(water, vpd))
                                                   // fluxes
                    output.AddRange(new object[] { prec_after_interception, prec_to_soil, et, mCanopy.evaporationCanopy(), mContent, mPsi[doy], excess });
                    // other states
                    output.Add(mSnowPack.snowPack());
                    //special sanity check:
                    if (prec_to_soil > 0.0 && mCanopy.interception() > 0.0)
                    {
                        if (mSnowPack.snowPack() == 0.0 && day.preciptitation == 0)
                        {
                            Debug.WriteLine("watercontent increase without precipititation");
                        }
                    }
                }
                //); // DBGMODE()
            }
            // call external modules
            GlobalSettings.instance().model().modules().calculateWater(mRU, add_data);
            mLastYear = GlobalSettings.instance().currentYear();
        }
    }
}