using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tools;
using iLand.Tree;
using System;

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
        private float mPsi_koeff_b; // see psiFromHeight()
        private float mPsi_sat; // see psiFromHeight(), kPa
        private float mTheta_sat; // see psiFromHeight(), [-], m3/m3
        private readonly ResourceUnit mRU; // resource unit to which this watercycle is connected
        private readonly SnowPack mSnowPack; // object representing the snow cover (aggregation, melting)
        private float mPermanentWiltingPoint; // bucket "height" of PWP (is fixed to -4MPa) (mm)
        private float mLaiNeedle;
        private float mLaiBroadleaved;

        public Canopy Canopy { get; private set; } // object representing the forest canopy (interception, evaporation)
        // TODO: should conductance move to Canopy class?
        public float CanopyConductance { get; private set; } // current canopy conductance (LAI weighted CC of available tree species) (m/s)
        public float CurrentSoilWaterContent { get; private set; } // current water content in mm water column of the soil
        public float FieldCapacity { get; private set; } //  bucket height of field-capacity (eq. -15kPa) (mm)
        public float[] SoilWaterPsi { get; private set; } // soil water potential for each day in kPa

        public float SoilDepth { get; private set; } // soil depth (without rocks) in mm
        public double SnowDays { get; set; } // # of days with snowcover >0
        public double SnowDayRadiation { get; set; } // sum of radiation input (MJ/m2) for days with snow cover (used in albedo calculations)
        public double TotalEvapotranspiration { get; set; } // annual sum of evapotranspiration (mm)
        public double TotalRunoff { get; set; } // annual sum of water loss due to lateral outflow/groundwater flow (mm)

        public WaterCycle(Project projectFile, ResourceUnit ru)
        {
            this.mRU = ru;
            this.mSnowPack = new SnowPack();

            this.Canopy = new Canopy(projectFile.Model.Settings.AirDensity);
            this.SoilDepth = 0;
            this.SoilWaterPsi = new float[Constant.DaysInLeapYear];
        }

        public double CurrentSnowWaterEquivalent() { return mSnowPack.WaterEquivalent; } // current water stored as snow (mm water)
        
        public void SetContent(float soilWaterInMM, float snowWaterEquivalentInMM)
        { 
            this.CurrentSoilWaterContent = soilWaterInMM; 
            this.mSnowPack.WaterEquivalent = snowWaterEquivalentInMM; 
        }

        public void Setup(Project projectFile, EnvironmentReader environmentReader)
        {
            // get values...
            this.FieldCapacity = 0.0F; // on top
            this.SoilDepth = 10.0F * environmentReader.CurrentSoilDepth; // convert from cm to mm TODO: zero is not a realistic default
            float percentSand = environmentReader.CurrentSoilSand;
            float percentSilt = environmentReader.CurrentSoilSilt;
            float percentClay = environmentReader.CurrentSoilClay;
            if (Math.Abs(100.0 - (percentSand + percentSilt + percentClay)) > 0.01)
            {
                throw new NotSupportedException(String.Format("Setup WaterCycle: soil textures do not sum to 100% within 0.01%. Sand: {0}%, silt: {1}%, clay: {2}%. Are these values specified in /project/model/site?", percentSand, percentSilt, percentClay));
            }

            // calculate soil characteristics based on empirical functions (Schwalm & Ek, 2004)
            // note: the variables are percentages [0..100]
            this.mPsi_sat = -0.000098F * MathF.Exp((1.54F - 0.0095F * percentSand + 0.0063f * percentSilt) * MathF.Log(10.0F)); // Eq. 83
            this.mPsi_koeff_b = -(3.1F + 0.157F * percentClay - 0.003F * percentSand);  // Eq. 84
            this.mTheta_sat = 0.01F * (50.5F - 0.142F * percentSand - 0.037F * percentClay); // Eq. 78

            this.mPermanentWiltingPoint = this.GetSoilWaterContentFromPsi(-4000.0F); // maximum psi is set to a constant of -4MPa

            if (projectFile.Model.Settings.WaterUseSoilSaturation == false) // TODO: should be on ModelSettings, why does this default to false?
            {
                this.FieldCapacity = this.GetSoilWaterContentFromPsi(-15.0F);
                if (this.FieldCapacity < this.mPermanentWiltingPoint)
                {
                    throw new NotSupportedException("Field capacity is below permanent wilting point. Consider setting useSoilSaturation = true.");
                }
            }
            else
            {
                // =-EXP((1.54-0.0095* pctSand +0.0063* pctSilt)*LN(10))*0.000098
                float psiSaturation = -0.000098F * MathF.Exp((1.54F - 0.0095F * percentSand + 0.0063F * percentSilt) * MathF.Log(10.0F));
                this.FieldCapacity = this.GetSoilWaterContentFromPsi(psiSaturation);
                //if (model.Files.LogInfo())
                //{
                //    Debug.WriteLine("psi: saturation " + psiSaturation + " field capacity: " + FieldCapacity);
                //}
            }

            // start with full soil water content (in the middle of winter)
            // BUGBUG: depends on site that is being modeled and whether simulation is configured to start at a time when soils are saturated
            this.CurrentSoilWaterContent = this.FieldCapacity;
            //if (model.Files.LogDebug())
            //{
            //    Debug.WriteLine("setup of water: Psi_sat (kPa) " + mPsi_sat + " Theta_sat " + mTheta_sat + " coeff. b " + mPsi_koeff_b);
            //}
            this.CanopyConductance = 0.0F;

            // canopy settings
            this.Canopy.NeedleStorageFactor = projectFile.Model.Settings.InterceptionStorageNeedle;
            this.Canopy.DecidousStorageFactor = projectFile.Model.Settings.InterceptionStorageBroadleaf;
            this.mSnowPack.MeltTemperature = projectFile.Model.Settings.SnowMeltTemperature;

            this.TotalEvapotranspiration = this.TotalRunoff = this.SnowDayRadiation = 0.0;
            this.SnowDays = 0;
        }

        /** function to calculate the water pressure [saugspannung] for a given amount of water.
            returns water potential in kPa.
          see http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance */
        public float GetPsiFromSoilWaterContent(float soilWaterContentInMM)
        {
            // psi_x = psi_ref * ( rho_x / rho_ref) ^ b
            if (soilWaterContentInMM < 0.001F)
            {
                return -100000000.0F;
            }
            float psi_x = mPsi_sat * MathF.Pow(soilWaterContentInMM / this.SoilDepth / this.mTheta_sat, this.mPsi_koeff_b);
            return psi_x; // pis
        }

        /// calculate the height of the water column for a given pressure
        /// return water amount in mm
        /// see http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance
        public float GetSoilWaterContentFromPsi(float psiInKilopascals)
        {
            // rho_x = rho_ref * (psi_x / psi_ref)^(1/b)
            float mmH20 = this.SoilDepth * this.mTheta_sat * MathF.Pow(psiInKilopascals / this.mPsi_sat, 1.0F / this.mPsi_koeff_b);
            return mmH20;
        }

        /// get canopy characteristics of the resource unit.
        /// It is important, that species-statistics are valid when this function is called (LAI)!
        private void GetStandValues(Project projectFile)
        {
            this.mLaiNeedle = 0.0F;
            this.mLaiBroadleaved = 0.0F;
            this.CanopyConductance = 0.0F;
            const float groundVegetationCC = 0.02F;
            foreach (ResourceUnitTreeSpecies ruSpecies in this.mRU.Trees.SpeciesAvailableOnResourceUnit) 
            {
                float lai = ruSpecies.Statistics.LeafAreaIndex[^2]; // use previous year's LAI as this year's hasn't yet been computed
                if (ruSpecies.Species.IsConiferous)
                {
                    mLaiNeedle += lai;
                }
                else
                {
                    mLaiBroadleaved += lai;
                }
                this.CanopyConductance += ruSpecies.Species.MaxCanopyConductance * lai; // weigh with LAI
            }
            float totalLai = mLaiBroadleaved + mLaiNeedle;

            // handle cases with LAI < 1 (use generic "ground cover characteristics" instead)
            /* The LAI used here is derived from the "stockable" area (and not the stocked area).
               If the stand has gaps, the available trees are "thinned" across the whole area. Otherwise (when stocked area is used)
               the LAI would overestimate the transpiring canopy. However, the current solution overestimates e.g. the interception.
               If the "thinned out" LAI is below one, the rest (i.e. the gaps) are thought to be covered by ground vegetation.
            */
            if (totalLai < 1.0F)
            {
                this.CanopyConductance += groundVegetationCC * (1.0F - totalLai);
                totalLai = 1.0F;
            }
            this.CanopyConductance /= totalLai;

            if (totalLai < projectFile.Model.Settings.LaiThresholdForClosedStands)
            {
                // following Landsberg and Waring: when LAI is < 3 (default for laiThresholdForClosedStands), a linear "ramp" from 0 to 3 is assumed
                // http://iland.boku.ac.at/water+cycle#transpiration_and_canopy_conductance
                this.CanopyConductance *= totalLai / projectFile.Model.Settings.LaiThresholdForClosedStands;
            }
        }

        /// calculate responses for ground vegetation, i.e. for "unstocked" areas.
        /// this duplicates calculations done in Species.
        /// @return Minimum of vpd and soilwater response for default
        private static float GetLimitingWaterVpdResponse(float psiInKilopascals, float vpdInKilopascals)
        {
            // constant parameters used for ground vegetation:
            const float mPsiMin = 1.5F; // MPa
            const float mRespVpdExponent = -0.6F;
            // see TreeSpecies.GetSoilWaterResponse() and ResourceUnitTreeSpeciesResponse.GetLimitingSoilWaterOrVpdResponse()
            float psiMPa = 0.001F * psiInKilopascals; // convert to MPa
            float waterResponse = Maths.Limit(1.0F - psiMPa / mPsiMin, 0.0F, 1.0F);
            // see Species.GetVpdResponse()

            float vpdResponse = MathF.Exp(mRespVpdExponent * vpdInKilopascals);
            return Math.Min(waterResponse, vpdResponse);
        }

        /// Calculate combined VPD and soil water response for all species on the RU. This is used for calculation of the transpiration.
        private float GetSoilAtmosphereResponse(float psiInKilopascals, float vpdInKilopascals)
        {
            float soilAtmosphereResponse = 0.0F; // LAI weighted minimum response for all speices on the RU
            float totalLaiFactor = 0.0F;
            foreach (ResourceUnitTreeSpecies ruSpecies in this.mRU.Trees.SpeciesAvailableOnResourceUnit)
            {
                if (ruSpecies.LaiFraction > 0.0F)
                {
                    // retrieve the minimum of VPD / soil water response for that species
                    ruSpecies.Response.GetLimitingSoilWaterOrVpdResponse(psiInKilopascals, vpdInKilopascals, out float limitingResponse);
                    soilAtmosphereResponse += limitingResponse * ruSpecies.LaiFraction;
                    totalLaiFactor += ruSpecies.LaiFraction;
                }
            }

            if (totalLaiFactor < 1.0F)
            {
                // the LAI is below 1: the rest is considered as "ground vegetation"
                soilAtmosphereResponse += WaterCycle.GetLimitingWaterVpdResponse(psiInKilopascals, vpdInKilopascals) * (1.0F - totalLaiFactor);
            }

            // add an aging factor to the total response (averageAging: leaf area weighted mean aging value):
            // conceptually: response = min(vpd_response, water_response)*aging
            if (totalLaiFactor == 1.0F)
            {
                soilAtmosphereResponse *= mRU.Trees.AverageLeafAreaWeightedAgingFactor; // no ground cover: use aging value for all LA
            }
            else if (totalLaiFactor > 0.0F && mRU.Trees.AverageLeafAreaWeightedAgingFactor > 0.0F)
            {
                soilAtmosphereResponse *= (1.0F - totalLaiFactor) * 1.0F + (totalLaiFactor * mRU.Trees.AverageLeafAreaWeightedAgingFactor); // between 0..1: a part of the LAI is "ground cover" (aging=1)
            }

            if (this.mRU.Trees.AverageLeafAreaWeightedAgingFactor > 1.0F || this.mRU.Trees.AverageLeafAreaWeightedAgingFactor < 0.0F || soilAtmosphereResponse < 0.0F || soilAtmosphereResponse > 1.0F)
            {
                throw new NotSupportedException("Average aging or soil atmosphere response invalid. Aging: " + mRU.Trees.AverageLeafAreaWeightedAgingFactor + ", soil-atmosphere response " + soilAtmosphereResponse + ", total LAI factor: " + totalLaiFactor + ".");
            }
            return soilAtmosphereResponse;
        }

        /// Main Water Cycle function. This function triggers all water related tasks for
        /// one simulation year.
        /// @sa http://iland.boku.ac.at/water+cycle
        public WaterCycleData RunYear(Project projectFile)
        {
            //using DebugTimer tw = model.DebugTimers.Create("WaterCycle.Run()");
            WaterCycleData hydrologicState = new WaterCycleData();

            // preparations (once a year)
            this.GetStandValues(projectFile); // fetch canopy characteristics from iLand (including weighted average for mCanopyConductance)
            this.Canopy.SetStandParameters(this.mLaiNeedle, this.mLaiBroadleaved, this.CanopyConductance);

            // main loop over all days of the year
            this.SnowDayRadiation = 0.0;
            this.SnowDays = 0;
            this.TotalEvapotranspiration = 0.0;
            this.TotalRunoff = 0.0;
            for (int dayIndex = this.mRU.Climate.CurrentJanuary1, dayOfYear = 0; dayIndex < this.mRU.Climate.NextJanuary1; ++dayIndex, ++dayOfYear)
            {
                ClimateDay day = this.mRU.Climate[dayIndex];
                // (2) interception by the crown
                float throughfallInMM = Canopy.Flow(day.Preciptitation);
                // (3) storage in the snow pack
                float infiltrationInMM = mSnowPack.Flow(throughfallInMM, day.MeanDaytimeTemperature);
                // save extra data (used by e.g. fire module)
                hydrologicState.WaterReachingGround[dayOfYear] = infiltrationInMM;
                hydrologicState.SnowCover[dayOfYear] = mSnowPack.WaterEquivalent;
                if (mSnowPack.WaterEquivalent > 0.0)
                {
                    this.SnowDayRadiation += day.Radiation;
                    ++this.SnowDays;
                }

                // (4) add rest to soil
                this.CurrentSoilWaterContent += infiltrationInMM;

                if (this.CurrentSoilWaterContent > this.FieldCapacity)
                {
                    // excess water runoff
                    double runoffInMM = this.CurrentSoilWaterContent - this.FieldCapacity;
                    this.TotalRunoff += runoffInMM;
                    this.CurrentSoilWaterContent = this.FieldCapacity;
                }

                float currentPsi = this.GetPsiFromSoilWaterContent(this.CurrentSoilWaterContent);
                this.SoilWaterPsi[dayOfYear] = currentPsi;

                // (5) transpiration of the vegetation (and of water intercepted in canopy)
                // calculate the LAI-weighted response values for soil water and vpd:
                double interception_before_transpiration = Canopy.Interception;
                float soilAtmosphereResponse = this.GetSoilAtmosphereResponse(currentPsi, day.Vpd);
                float et = this.Canopy.GetEvapotranspiration3PG(projectFile, day, this.mRU.Climate.GetDayLengthInHours(dayOfYear), soilAtmosphereResponse);
                // if there is some flow from intercepted water to the ground -> add to "water_to_the_ground"
                if (this.Canopy.Interception < interception_before_transpiration)
                {
                    hydrologicState.WaterReachingGround[dayOfYear] += interception_before_transpiration - this.Canopy.Interception;
                }

                this.CurrentSoilWaterContent -= et; // reduce content (transpiration)
                                                    // add intercepted water (that is *not* evaporated) again to the soil (or add to snow if temp too low -> call to snowpack)
                this.CurrentSoilWaterContent += this.mSnowPack.AddSnowWaterEquivalent(this.Canopy.Interception, day.MeanDaytimeTemperature);

                // do not remove water below the PWP (fixed value)
                if (this.CurrentSoilWaterContent < mPermanentWiltingPoint)
                {
                    et -= mPermanentWiltingPoint - this.CurrentSoilWaterContent; // reduce et (for bookkeeping)
                    this.CurrentSoilWaterContent = mPermanentWiltingPoint;
                }

                this.TotalEvapotranspiration += et;

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

            return hydrologicState;
        }
    }
}