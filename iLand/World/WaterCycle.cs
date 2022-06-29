using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tool;
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

        See http://iland-model.org/water+cycle
        */
    public class WaterCycle
    {
        private float laiBroadleaved;
        private float laiNeedle;
        private float residualSoilWater; // bucket "height" of PWP (is fixed to -4MPa) (mm)
        private readonly ResourceUnit ru; // resource unit to which this watercycle is connected
        private readonly SoilWaterRetention soilWaterRetention;
        private readonly SnowPack snowPack; // object representing the snow cover (aggregation, melting)

        public Canopy Canopy { get; private init; } // object representing the forest canopy (interception, evaporation)
        // TODO: should conductance move to Canopy class?
        public float CanopyConductance { get; private set; } // current canopy conductance (LAI weighted CC of available tree species) (m/s)
        
        public float CurrentSoilWaterContent { get; private set; } // current water content in mm water column of the soil
        public float FieldCapacity { get; private set; } //  bucket height of field-capacity (eq. -15kPa) (mm)
        public float SoilDepthInMM { get; private set; } // soil depth (without rocks) in mm
        public float[] SoilWaterPotentialByDay { get; private init; } // soil water potential for each day of year in kPa, needed for soil water modifier

        public float SnowDayRadiation { get; set; } // sum of radiation input (MJ/m2) for days with snow cover (used in albedo calculations)
        public float SnowDays { get; set; } // # of days with snowcover >0
        public float TotalEvapotranspiration { get; set; } // annual sum of evapotranspiration (mm)
        public float TotalRunoff { get; set; } // annual sum of water loss due to lateral outflow/groundwater flow (mm)

        public WaterCycle(Project projectFile, ResourceUnit ru)
        {
            this.laiBroadleaved = Single.NaN;
            this.laiNeedle = Single.NaN;
            this.residualSoilWater = Single.NaN;
            this.ru = ru;
            this.soilWaterRetention = new SoilWaterRetentionCampbell();
            this.snowPack = new();

            this.Canopy = new Canopy(projectFile.Model.Ecosystem.AirDensity);
            this.FieldCapacity = Single.NaN;
            this.SoilDepthInMM = Single.NaN;
            this.SoilWaterPotentialByDay = new float[Constant.DaysInLeapYear];

            this.SnowDayRadiation = Single.NaN;
            this.SnowDays = Single.NaN;
            this.TotalEvapotranspiration = Single.NaN;
            this.TotalRunoff = Single.NaN;
        }

        public float CurrentSnowWaterEquivalent() { return snowPack.WaterEquivalentInMM; } // current water stored as snow (mm water)
        
        public void SetContent(float soilWaterInMM, float snowWaterEquivalentInMM)
        { 
            this.CurrentSoilWaterContent = soilWaterInMM; 
            this.snowPack.WaterEquivalentInMM = snowWaterEquivalentInMM; 
        }

        public void Setup(Project projectFile, ResourceUnitReader environmentReader)
        {
            // get values...
            this.SoilDepthInMM = 10.0F * environmentReader.CurrentEnvironment.SoilDepthInCM; // convert from cm to mm TODO: zero is not a realistic default
            
            float psiSaturation = this.soilWaterRetention.Setup(environmentReader, projectFile.Model.Settings.WaterUseSoilSaturation);
            this.FieldCapacity = this.soilWaterRetention.GetSoilWaterContentFromPsi(this.SoilDepthInMM, psiSaturation);
            this.residualSoilWater = this.soilWaterRetention.GetSoilWaterContentFromPsi(this.SoilDepthInMM, -4000.0F); // maximum psi is set to a constant of -4 MPa

            if (projectFile.Model.Settings.WaterUseSoilSaturation == false) // TODO: should be on ModelSettings, why does this default to false?
            {
                if (this.FieldCapacity < this.residualSoilWater)
                {
                    throw new NotSupportedException("Field capacity is below permanent wilting point. Consider setting useSoilSaturation = true.");
                }
            }

            // start with full soil water content (in the middle of winter)
            // BUGBUG: depends on whether soils on site ever reach saturation and, if so, whether simulation starts at a time when soils are saturated
            this.CurrentSoilWaterContent = this.FieldCapacity;
            //if (model.Files.LogDebug())
            //{
            //    Debug.WriteLine("setup of water: Psi_sat (kPa) " + mPsi_sat + " Theta_sat " + mTheta_sat + " coeff. b " + mPsi_koeff_b);
            //}

            this.Canopy.BroadleafStorageInMM = projectFile.Model.Ecosystem.InterceptionStorageBroadleaf;
            this.Canopy.NeedleStorageInMM = projectFile.Model.Ecosystem.InterceptionStorageNeedle;
            this.CanopyConductance = 0.0F;

            this.SnowDays = 0;
            this.snowPack.MeltTemperatureInC = projectFile.Model.Ecosystem.SnowmeltTemperature;
            this.TotalEvapotranspiration = this.TotalRunoff = this.SnowDayRadiation = 0.0F;
        }

        /// get canopy characteristics of the resource unit.
        /// It is important, that species-statistics are valid when this function is called (LAI)!
        private void GetStandValues(Project projectFile)
        {
            this.laiNeedle = 0.0F;
            this.laiBroadleaved = 0.0F;
            this.CanopyConductance = 0.0F;
            const float groundVegetationCC = 0.02F;
            foreach (ResourceUnitTreeSpecies ruSpecies in this.ru.Trees.SpeciesAvailableOnResourceUnit) 
            {
                float lai = ruSpecies.Statistics.LeafAreaIndex; // use previous year's LAI as this year's hasn't yet been computed
                if (ruSpecies.Species.IsConiferous)
                {
                    laiNeedle += lai;
                }
                else
                {
                    laiBroadleaved += lai;
                }
                this.CanopyConductance += ruSpecies.Species.MaxCanopyConductance * lai; // weigh with LAI
            }
            float totalLai = laiBroadleaved + laiNeedle;

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

            if (totalLai < projectFile.Model.Ecosystem.LaiThresholdForConstantStandConductance)
            {
                // following Landsberg and Waring: when LAI is < 3 (default for laiThresholdForClosedStands), a linear "ramp" from 0 to 3 is assumed
                // http://iland-model.org/water+cycle#transpiration_and_canopy_conductance
                this.CanopyConductance *= totalLai / projectFile.Model.Ecosystem.LaiThresholdForConstantStandConductance;
            }
        }

        /// calculate responses for ground vegetation, i.e. for "unstocked" areas.
        /// this duplicates calculations done in Species.
        /// @return Minimum of vpd and soilwater response for default
        private static float GetLimitingWaterVpdModifier(float psiInKilopascals, float vpdInKilopascals)
        {
            // constant parameters used for ground vegetation:
            const float mPsiMin = 1.5F; // MPa
            const float mRespVpdExponent = -0.6F;
            // see TreeSpecies.GetSoilWaterModifier() and ResourceUnitTreeSpeciesResponse.GetLimitingSoilWaterOrVpdModifier()
            float psiMPa = 0.001F * psiInKilopascals; // convert to MPa
            float waterModifier = Maths.Limit(1.0F - psiMPa / mPsiMin, 0.0F, 1.0F);
            // see Species.GetVpdModifier()

            float vpdModifier = MathF.Exp(mRespVpdExponent * vpdInKilopascals);
            return Math.Min(waterModifier, vpdModifier);
        }

        /// Calculate combined VPD and soil water response for all species on the RU. This is used for calculation of the transpiration.
        private float GetSoilAtmosphereModifier(float psiInKilopascals, float vpdInKilopascals)
        {
            float soilAtmosphereModifier = 0.0F; // LAI weighted minimum response for all speices on the RU
            float totalLaiFactor = 0.0F;
            foreach (ResourceUnitTreeSpecies ruSpecies in this.ru.Trees.SpeciesAvailableOnResourceUnit)
            {
                if (ruSpecies.LaiFraction > 0.0F)
                {
                    // retrieve the minimum of VPD / soil water response for that species
                    ruSpecies.Response.GetLimitingSoilWaterOrVpdModifier(psiInKilopascals, vpdInKilopascals, out float limitingResponse);
                    soilAtmosphereModifier += limitingResponse * ruSpecies.LaiFraction;
                    totalLaiFactor += ruSpecies.LaiFraction;
                }
            }

            if (totalLaiFactor < 1.0F)
            {
                // the LAI is below 1: the rest is considered as "ground vegetation"
                soilAtmosphereModifier += WaterCycle.GetLimitingWaterVpdModifier(psiInKilopascals, vpdInKilopascals) * (1.0F - totalLaiFactor);
            }

            // add an aging factor to the total response (averageAging: leaf area weighted mean aging value):
            // conceptually: response = min(vpd_response, water_response)*aging
            if (totalLaiFactor == 1.0F)
            {
                soilAtmosphereModifier *= ru.Trees.AverageLeafAreaWeightedAgingFactor; // no ground cover: use aging value for all LA
            }
            else if (totalLaiFactor > 0.0F && ru.Trees.AverageLeafAreaWeightedAgingFactor > 0.0F)
            {
                soilAtmosphereModifier *= (1.0F - totalLaiFactor) * 1.0F + (totalLaiFactor * ru.Trees.AverageLeafAreaWeightedAgingFactor); // between 0..1: a part of the LAI is "ground cover" (aging=1)
            }

            if (this.ru.Trees.AverageLeafAreaWeightedAgingFactor > 1.0F || this.ru.Trees.AverageLeafAreaWeightedAgingFactor < 0.0F || soilAtmosphereModifier < 0.0F || soilAtmosphereModifier > 1.0F)
            {
                throw new NotSupportedException("Average aging or soil atmosphere response invalid. Aging: " + ru.Trees.AverageLeafAreaWeightedAgingFactor + ", soil-atmosphere response " + soilAtmosphereModifier + ", total LAI factor: " + totalLaiFactor + ".");
            }
            return soilAtmosphereModifier;
        }

        /// Main Water Cycle function. This function triggers all water related tasks for
        /// one simulation year.
        /// @sa http://iland-model.org/water+cycle
        public WaterCycleData RunYear(Project projectFile)
        {
            WaterCycleData hydrologicState = new();

            // preparations (once a year)
            this.GetStandValues(projectFile); // fetch canopy characteristics from iLand (including weighted average for mCanopyConductance)
            this.Canopy.SetStandParameters(this.laiNeedle, this.laiBroadleaved, this.CanopyConductance);

            // main loop over all days of the year
            this.SnowDayRadiation = 0.0F;
            this.SnowDays = 0;
            this.TotalEvapotranspiration = 0.0F;
            this.TotalRunoff = 0.0F;
            for (int dayIndex = this.ru.Climate.CurrentJanuary1, dayOfYear = 0; dayIndex < this.ru.Climate.NextJanuary1; ++dayIndex, ++dayOfYear)
            {
                ClimateDay day = this.ru.Climate[dayIndex];
                // (2) interception by the crown
                float throughfallInMM = this.Canopy.FlowDayToStorage(day.Preciptitation);
                // (3) storage in the snow pack
                float infiltrationInMM = this.snowPack.FlowDay(throughfallInMM, day.MeanDaytimeTemperature);
                // save extra data (used by e.g. fire module)
                hydrologicState.WaterReachingGround[dayOfYear] = infiltrationInMM;
                hydrologicState.SnowCover[dayOfYear] = this.snowPack.WaterEquivalentInMM;
                if (this.snowPack.WaterEquivalentInMM > 0.0)
                {
                    this.SnowDayRadiation += day.Radiation;
                    ++this.SnowDays;
                }

                // (4) add rest to soil
                this.CurrentSoilWaterContent += infiltrationInMM;

                if (this.CurrentSoilWaterContent > this.FieldCapacity)
                {
                    // excess water runoff
                    float runoffInMM = this.CurrentSoilWaterContent - this.FieldCapacity;
                    this.TotalRunoff += runoffInMM;
                    this.CurrentSoilWaterContent = this.FieldCapacity;
                }

                float currentPsi = this.soilWaterRetention.GetSoilWaterPotentialFromWaterContent(this.SoilDepthInMM, this.CurrentSoilWaterContent);
                this.SoilWaterPotentialByDay[dayOfYear] = currentPsi;

                // (5) transpiration of the vegetation and of water intercepted in canopy
                // implicit assumption: water does not remain in canopy between days
                // calculate the LAI-weighted response values for soil water and vpd:
                float interceptionBeforeTranspiration = this.Canopy.StoredWaterInMM;
                float soilAtmosphereResponse = this.GetSoilAtmosphereModifier(currentPsi, day.Vpd);
                float dayLengthInHours = this.ru.Climate.Sun.GetDayLengthInHours(dayOfYear);
                float evapotranspirationInMM = this.Canopy.FlowDayEvapotranspiration3PG(projectFile, day, dayLengthInHours, soilAtmosphereResponse);
                // if there is some flow from intercepted water to the ground -> add to "water_to_the_ground"
                if (this.Canopy.StoredWaterInMM < interceptionBeforeTranspiration)
                {
                    float stemflow = interceptionBeforeTranspiration - this.Canopy.StoredWaterInMM;
                    hydrologicState.WaterReachingGround[dayOfYear] += stemflow;
                }

                this.CurrentSoilWaterContent -= evapotranspirationInMM; // reduce content (transpiration)
                // add intercepted water (that is *not* evaporated) again to the soil (or add to snow if temp too low -> call to snowpack)
                this.CurrentSoilWaterContent += this.snowPack.AddSnowWaterEquivalent(this.Canopy.StoredWaterInMM, day.MeanDaytimeTemperature);

                // do not remove water below the PWP (fixed value)
                if (this.CurrentSoilWaterContent < this.residualSoilWater)
                {
                    evapotranspirationInMM -= this.residualSoilWater - this.CurrentSoilWaterContent; // reduce et (for bookkeeping)
                    this.CurrentSoilWaterContent = this.residualSoilWater;
                }

                this.TotalEvapotranspiration += evapotranspirationInMM;

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