using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using System;
using System.Diagnostics;

namespace iLand.World
{
    /** @class WaterCycle
        simulates the water cycle on a ResourceUnit.
        The WaterCycle is simulated with a daily time step on the spatial level of a ResourceUnit. Related are
        the snow module (SnowPack), and Canopy module that simulates the interception (and evaporation) of precipitation and the
        transpiration from the canopy.
        The WaterCycle covers the "soil water bucket". Main entry function is run().

        See http://iland.boku.ac.at/water+cycle
        */
    public class WaterCycle
    {
        private int mLastYear; // last year of execution
        private double mPsi_koeff_b; // see psiFromHeight()
        private double mPsi_sat; // see psiFromHeight(), kPa
        private double mTheta_sat; // see psiFromHeight(), [-], m3/m3
        private ResourceUnit mRU; // resource unit to which this watercycle is connected
        private readonly Canopy mCanopy; // object representing the forest canopy (interception, evaporation)
        private readonly SnowPack mSnowPack; // object representing the snow cover (aggregation, melting)
        private double mPermanentWiltingPoint; // bucket "height" of PWP (is fixed to -4MPa) (mm)
        private double mLAINeedle;
        private double mLAIBroadleaved;
        
        public double CanopyConductance { get; private set; } // current canopy conductance (LAI weighted CC of available tree species) (m/s)
        public double CurrentSoilWaterContent { get; private set; } // current water content in mm water column of the soil
        public double FieldCapacity { get; private set; } //  bucket height of field-capacity (eq. -15kPa) (mm)
        public double[] Psi { get; private set; } // soil water potential for each day in kPa

        public double SoilDepth { get; private set; } // soil depth (without rocks) in mm
        public double SnowDays { get; set; } // # of days with snowcover >0
        public double SnowDayRad { get; set; } // sum of radiation input (MJ/m2) for days with snow cover (used in albedo calculations)
        public double TotalEvapotranspiration { get; set; } // annual sum of evapotranspiration (mm)
        public double TotalWaterLoss { get; set; } // annual sum of water loss due to lateral outflow/groundwater flow (mm)

        public WaterCycle()
        {
            this.mCanopy = new Canopy();
            this.mLastYear = -1;
            this.Psi = new double[Constant.DaysInLeapYear];
            this.mSnowPack = new SnowPack();
            this.SoilDepth = 0;
        }

        public double CurrentSnowWaterEquivalent() { return mSnowPack.WaterEquivalent; } // current water stored as snow (mm water)
        /// monthly values for PET (mm sum)
        public double[] ReferenceEvapotranspiration() { return mCanopy.ReferenceEvapotranspiration; }
        public void SetContent(double content, double snow_mm) { CurrentSoilWaterContent = content; mSnowPack.WaterEquivalent = snow_mm; }

        public void Setup(Model model, ResourceUnit ru)
        {
            mRU = ru;
            // get values...
            FieldCapacity = 0.0; // on top
            this.SoilDepth = 10 * model.Environment.CurrentSoilDepth.Value; // convert from cm to mm TODO: zero is not a realistic default
            double pct_sand = model.Environment.CurrentSoilSand.Value;
            double pct_silt = model.Environment.CurrentSoilSilt.Value;
            double pct_clay = model.Environment.CurrentSoilClay.Value;
            if (Math.Abs(100.0 - (pct_sand + pct_silt + pct_clay)) > 0.01)
            {
                throw new NotSupportedException(String.Format("Setup WaterCycle: soil textures do not sum to 100% within 0.01%. Sand: {0}%, silt: {1}%, clay: {2}%. Are these values specified in /project/model/site?", pct_sand, pct_silt, pct_clay));
            }

            // calculate soil characteristics based on empirical functions (Schwalm & Ek, 2004)
            // note: the variables are percentages [0..100]
            mPsi_sat = -Math.Exp((1.54 - 0.0095 * pct_sand + 0.0063 * pct_silt) * Math.Log(10)) * 0.000098; // Eq. 83
            mPsi_koeff_b = -(3.1 + 0.157 * pct_clay - 0.003 * pct_sand);  // Eq. 84
            mTheta_sat = 0.01 * (50.5 - 0.142 * pct_sand - 0.037 * pct_clay); // Eq. 78
            mCanopy.Setup(model);

            mPermanentWiltingPoint = HeightFromPsi(-4000); // maximum psi is set to a constant of -4MPa

            if (model.Project.Model.Settings.WaterUseSoilSaturation == false) // TODO: should be on ModelSettings, why does this default to false?
            {
                // BUGBUG: may result in field capacity height below permanent wilt point
                FieldCapacity = HeightFromPsi(-15);
            }
            else
            {
                // =-EXP((1.54-0.0095* pctSand +0.0063* pctSilt)*LN(10))*0.000098
                double psi_sat = -Math.Exp((1.54 - 0.0095 * pct_sand + 0.0063 * pct_silt) * Math.Log(10.0)) * 0.000098;
                FieldCapacity = HeightFromPsi(psi_sat);
                if (model.Files.LogInfo())
                {
                    Debug.WriteLine("psi: saturation " + psi_sat + " field capacity: " + FieldCapacity);
                }
            }

            // start with full soil water content (in the middle of winter)
            // BUGBUG: depends on model location and input climate data
            CurrentSoilWaterContent = FieldCapacity;
            if (model.Files.LogDebug())
            {
                Debug.WriteLine("setup of water: Psi_sat (kPa) " + mPsi_sat + " Theta_sat " + mTheta_sat + " coeff. b " + mPsi_koeff_b);
            }
            CanopyConductance = 0.0;
            mLastYear = -1;

            // canopy settings
            mCanopy.NeedleFactor = model.Project.Model.Settings.InterceptionStorageNeedle;
            mCanopy.DecidousFactor = model.Project.Model.Settings.InterceptionStorageBroadleaf;
            mSnowPack.Temperature = model.Project.Model.Settings.SnowMeltTemperature;

            TotalEvapotranspiration = TotalWaterLoss = SnowDayRad = 0.0;
            SnowDays = 0;
        }

        /** function to calculate the water pressure [saugspannung] for a given amount of water.
            returns water potential in kPa.
          see http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance */
        public double PsiFromHeight(double mm)
        {
            // psi_x = psi_ref * ( rho_x / rho_ref) ^ b
            if (mm < 0.001)
            {
                return -100000000.0;
            }
            double psi_x = mPsi_sat * Math.Pow((mm / SoilDepth / mTheta_sat), mPsi_koeff_b);
            return psi_x; // pis
        }

        /// calculate the height of the water column for a given pressure
        /// return water amount in mm
        /// see http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance
        public double HeightFromPsi(double psi_kpa)
        {
            // rho_x = rho_ref * (psi_x / psi_ref)^(1/b)
            double h = SoilDepth * mTheta_sat * Math.Pow(psi_kpa / mPsi_sat, 1.0 / mPsi_koeff_b);
            return h;
        }

        /// get canopy characteristics of the resource unit.
        /// It is important, that species-statistics are valid when this function is called (LAI)!
        private void GetStandValues(Model model)
        {
            mLAINeedle = mLAIBroadleaved = 0.0;
            CanopyConductance = 0.0;
            double ground_vegetationCC = 0.02;
            foreach (ResourceUnitSpecies rus in mRU.Species) 
            {
                double lai = rus.Statistics.LeafAreaIndex;
                if (rus.Species.IsConiferous)
                {
                    mLAINeedle += lai;
                }
                else
                {
                    mLAIBroadleaved += lai;
                }
                CanopyConductance += rus.Species.MaxCanopyConductance * lai; // weigh with LAI
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
                CanopyConductance += (ground_vegetationCC) * (1.0 - total_lai);
                total_lai = 1.0;
            }
            CanopyConductance /= total_lai;

            if (total_lai < model.ModelSettings.LaiThresholdForClosedStands)
            {
                // following Landsberg and Waring: when LAI is < 3 (default for laiThresholdForClosedStands), a linear "ramp" from 0 to 3 is assumed
                // http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance
                CanopyConductance *= total_lai / model.ModelSettings.LaiThresholdForClosedStands;
            }
            if (model.Files.LogDebug())
            {
                Debug.WriteLine("WaterCycle.GetStandValues(): LAI needle " + mLAINeedle + " LAI Broadl: " + mLAIBroadleaved + " weighted avg. Conductance (m/2): " + CanopyConductance);
            }
        }

        /// calculate responses for ground vegetation, i.e. for "unstocked" areas.
        /// this duplicates calculations done in Species.
        /// @return Minimum of vpd and soilwater response for default
        public double CalculateBaseSoilAtmosphereResponse(double psi_kpa, double vpd_kpa)
        {
            // constant parameters used for ground vegetation:
            double mPsiMin = 1.5; // MPa
            double mRespVpdExponent = -0.6;
            // see SpeciesResponse::soilAtmosphereResponses()
            double water_resp;
            // see Species::soilwaterResponse:
            double psi_mpa = psi_kpa / 1000.0; // convert to MPa
            water_resp = Global.Limit(1.0 - psi_mpa / mPsiMin, 0.0, 1.0);
            // see species::vpdResponse

            double vpd_resp;
            vpd_resp = Math.Exp(mRespVpdExponent * vpd_kpa);
            return Math.Min(water_resp, vpd_resp);
        }

        /// calculate combined VPD and soilwaterresponse for all species
        /// on the RU. This is used for the calc. of the transpiration.
        public double CalculateSoilAtmosphereResponse(double psi_kpa, double vpd_kpa)
        {
            double total_response = 0; // LAI weighted minimum response for all speices on the RU
            double total_lai_factor = 0.0;
            foreach (ResourceUnitSpecies rus in mRU.Species)
            {
                if (rus.LaiFraction > 0.0)
                {
                    // retrieve the minimum of VPD / soil water response for that species
                    rus.Response.SoilAtmosphereResponses(psi_kpa, vpd_kpa, out double min_response);
                    total_response += min_response * rus.LaiFraction;
                    total_lai_factor += rus.LaiFraction;
                }
            }

            if (total_lai_factor < 1.0)
            {
                // the LAI is below 1: the rest is considered as "ground vegetation"
                total_response += CalculateBaseSoilAtmosphereResponse(psi_kpa, vpd_kpa) * (1.0 - total_lai_factor);
            }

            // add an aging factor to the total response (averageAging: leaf area weighted mean aging value):
            // conceptually: response = min(vpd_response, water_response)*aging
            if (total_lai_factor == 1.0)
            {
                total_response *= mRU.AverageAging; // no ground cover: use aging value for all LA
            }
            else if (total_lai_factor > 0.0 && mRU.AverageAging > 0.0)
            {
                total_response *= (1.0 - total_lai_factor) * 1.0 + (total_lai_factor * mRU.AverageAging); // between 0..1: a part of the LAI is "ground cover" (aging=1)
            }

#if DEBUG
            if (mRU.AverageAging > 1.0 || mRU.AverageAging < 0.0 || total_response < 0 || total_response > 1.0)
            {
                Debug.WriteLine("water cycle: average aging invalid. aging: " + mRU.AverageAging + " total response " + total_response + " total lai factor: " + total_lai_factor);
            }
#endif
            //DBG_IF(mRU.averageAging()>1. || mRU.averageAging()<0.,"water cycle", "average aging invalid!" );
            return total_response;
        }

        /// Main Water Cycle function. This function triggers all water related tasks for
        /// one simulation year.
        /// @sa http://iland.boku.ac.at/water+cycle
        public void Run(Model model)
        {
            // necessary?
            if (model.ModelSettings.CurrentYear == mLastYear)
            {
                return;
            }
            //using DebugTimer tw = model.DebugTimers.Create("WaterCycle.Run()");
            WaterCycleData hydrologicState = new WaterCycleData();

            // preparations (once a year)
            GetStandValues(model); // fetch canopy characteristics from iLand (including weighted average for mCanopyConductance)
            mCanopy.SetStandParameters(mLAINeedle, mLAIBroadleaved, CanopyConductance);

            // main loop over all days of the year
            Climate climate = mRU.Climate;
            int dayOfYear = 0;
            SnowDayRad = 0.0;
            SnowDays = 0;
            TotalEvapotranspiration = 0.0;
            TotalWaterLoss = 0.0;
            for (int dayIndex = climate.CurrentJanuary1; dayIndex < climate.NextJanuary1; ++dayIndex, ++dayOfYear)
            {
                ClimateDay day = climate[dayIndex];
                // (2) interception by the crown
                double throughfallInMM = mCanopy.Flow(day.Preciptitation);
                // (3) storage in the snow pack
                double infiltrationInMM = mSnowPack.Flow(throughfallInMM, day.MeanDaytimeTemperature);
                // save extra data (used by e.g. fire module)
                hydrologicState.WaterReachingGround[dayOfYear] = infiltrationInMM;
                hydrologicState.SnowCover[dayOfYear] = mSnowPack.WaterEquivalent;
                if (mSnowPack.WaterEquivalent > 0.0)
                {
                    SnowDayRad += day.Radiation;
                    SnowDays++;
                }

                // (4) add rest to soil
                CurrentSoilWaterContent += infiltrationInMM;

                if (CurrentSoilWaterContent > FieldCapacity)
                {
                    // excess water runoff
                    double runoffInMM = CurrentSoilWaterContent - FieldCapacity;
                    TotalWaterLoss += runoffInMM;
                    CurrentSoilWaterContent = FieldCapacity;
                }

                double currentPsi = PsiFromHeight(CurrentSoilWaterContent);
                Psi[dayOfYear] = currentPsi;

                // (5) transpiration of the vegetation (and of water intercepted in canopy)
                // calculate the LAI-weighted response values for soil water and vpd:
                double interception_before_transpiration = mCanopy.Interception;
                double combined_response = CalculateSoilAtmosphereResponse(currentPsi, day.Vpd);
                double et = mCanopy.Evapotranspiration3PG(day, model, climate.DayLengthInHours(dayOfYear), combined_response);
                // if there is some flow from intercepted water to the ground -> add to "water_to_the_ground"
                if (mCanopy.Interception < interception_before_transpiration)
                {
                    hydrologicState.WaterReachingGround[dayOfYear] += interception_before_transpiration - mCanopy.Interception;
                }

                CurrentSoilWaterContent -= et; // reduce content (transpiration)
                                // add intercepted water (that is *not* evaporated) again to the soil (or add to snow if temp too low -> call to snowpack)
                CurrentSoilWaterContent += mSnowPack.Add(mCanopy.Interception, day.MeanDaytimeTemperature);

                // do not remove water below the PWP (fixed value)
                if (CurrentSoilWaterContent < mPermanentWiltingPoint)
                {
                    et -= mPermanentWiltingPoint - CurrentSoilWaterContent; // reduce et (for bookkeeping)
                    CurrentSoilWaterContent = mPermanentWiltingPoint;
                }

                TotalEvapotranspiration += et;

                //DBGMODE(
                //if (model.GlobalSettings.IsDebugEnabled(DebugOutputs.WaterCycle))
                //{
                //    List<object> output = model.GlobalSettings.DebugList(day.ID(), DebugOutputs.WaterCycle);
                //    // climatic variables
                //    output.AddRange(new object[] { day.ID(), mRU.Index, mRU.ID, day.MeanDaytimeTemperature, day.Vpd, day.Preciptitation, day.Radiation });
                //    output.Add(combined_response); // combined response of all species on RU (min(water, vpd))
                //                                   // fluxes
                //    output.AddRange(new object[] { throughfallInMM, infiltrationInMM, et, mCanopy.EvaporationCanopy, CurrentSoilWaterContent, Psi[dayOfYear], runoffInMM });
                //    // other states
                //    output.Add(mSnowPack.WaterEquivalent);
                //    //special sanity check:
                //    if (infiltrationInMM > 0.0 && mCanopy.Interception > 0.0)
                //    {
                //        if (mSnowPack.WaterEquivalent == 0.0 && day.Preciptitation == 0)
                //        {
                //            Debug.WriteLine("watercontent increase without precipititation");
                //        }
                //    }
                //}
                //); // DBGMODE()
            }

            // call external modules
            model.Modules.CalculateWater(mRU, hydrologicState);
            mLastYear = model.ModelSettings.CurrentYear;
        }
    }
}