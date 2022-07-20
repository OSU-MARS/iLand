using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tool;
using iLand.Tree;
using System;

namespace iLand.World
{
    /** simulates the water cycle on a ResourceUnit.
        The WaterCycle is simulated with a daily time step on the spatial level of a ResourceUnit. Related are
        the snow module (SnowPack), and Canopy module that simulates the interception (and evaporation) of precipitation and the
        transpiration from the canopy.
        The WaterCycle covers the "soil water bucket". Main entry function is run().

        See http://iland-model.org/water+cycle
        */
    public class WaterCycle
    {
        private float residualSoilWater; // bucket "height" of PWP (is fixed to -4MPa) (mm)
        private readonly ResourceUnit ru; // resource unit to which this watercycle is connected
        private SoilWaterRetention? soilWaterRetention;
        private readonly SnowPack snowPack; // object representing the snow cover (aggregation, melting)

        public Canopy Canopy { get; private init; } // object representing the forest canopy (interception, evaporation)
        // TODO: should conductance move to Canopy class?
        public float CanopyConductance { get; private set; } // current canopy conductance (LAI weighted CC of available tree species) (m/s)
        
        public float CurrentSoilWater { get; private set; } // current water content in mm water column of the soil
        public float FieldCapacity { get; private set; } //  bucket height of field-capacity (eq. -15kPa) (mm)
        public float[] SoilWaterPotentialByWeatherTimestep { get; private init; } // soil water potential for each day of year in kPa, needed for soil water modifier

        public float SnowDayRadiation { get; set; } // sum of radiation input (MJ/m2) for days with snow cover (used in albedo calculations)
        public float SnowDays { get; set; } // number of days with snowcover > 0
        public float TotalEvapotranspiration { get; set; } // annual sum of evapotranspiration (mm)
        public float TotalRunoff { get; set; } // annual sum of water loss due to lateral outflow/groundwater flow (mm)

        public WaterCycle(Project projectFile, ResourceUnit ru)
        {
            this.residualSoilWater = Single.NaN;
            this.ru = ru;
            this.soilWaterRetention = null;
            this.snowPack = new();

            this.Canopy = new Canopy(projectFile.Model.Ecosystem.AirDensity);
            this.FieldCapacity = Single.NaN;
            this.SoilWaterPotentialByWeatherTimestep = new float[Constant.DaysInLeapYear]; // TODO: be able to allocate monthly length for monthly weather series

            this.SnowDayRadiation = Single.NaN;
            this.SnowDays = Single.NaN;
            this.TotalEvapotranspiration = Single.NaN;
            this.TotalRunoff = Single.NaN;
        }

        public float CurrentSnowWaterEquivalent() 
        {
            // current water stored as snow (mm water)
            return snowPack.WaterEquivalentInMM; 
        }
        
        public void SetContent(float soilWaterInMM, float snowWaterEquivalentInMM)
        { 
            this.CurrentSoilWater = soilWaterInMM; 
            this.snowPack.WaterEquivalentInMM = snowWaterEquivalentInMM; 
        }

        public void Setup(Project projectFile, ResourceUnitEnvironment environment)
        {
            // soil water
            this.soilWaterRetention = SoilWaterRetention.Create(environment);
            float psiSaturation = projectFile.Model.Settings.SoilSaturationPotentialInKPa; // kPa
            if (Single.IsNaN(psiSaturation))
            {
                psiSaturation = this.soilWaterRetention.SaturationPotentialInKPa;
            }
            this.FieldCapacity = this.soilWaterRetention.GetSoilWaterFromPotential(psiSaturation);
            this.residualSoilWater = this.soilWaterRetention.GetSoilWaterFromPotential(projectFile.Model.Settings.SoilPermanentWiltPotentialInKPA);

            if (this.FieldCapacity < this.residualSoilWater)
            {
                throw new NotSupportedException("Field capacity is below permanent wilting point. This indicates an internal error in the soil water retention curve or, if the soil saturation potential is specified in the project file (model.settings.soilSaturationPotential), that the saturation potential is too negative.");
            }

            // start with full soil water content (in the middle of winter)
            // BUGBUG: depends on whether soils on site ever reach saturation and, if so, whether simulation starts at a time when soils are saturated
            this.CurrentSoilWater = this.FieldCapacity;
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

        // get canopy characteristics of the resource unit.
        // It is important species statistics are valid when this function is called (LAI)!
        private (float laiConifer, float laiBroadleaf, float laiWeightedCanopyConductance) OnStartYear(Project projectFile)
        {
            float laiConifer = 0.0F;
            float laiBroadleaf = 0.0F;
            float laiWeightedCanopyConductance = 0.0F;
            foreach (ResourceUnitTreeSpecies ruSpecies in this.ru.Trees.SpeciesAvailableOnResourceUnit) 
            {
                float lai = ruSpecies.Statistics.LeafAreaIndex; // use previous year's LAI as this year's hasn't yet been computed
                if (ruSpecies.Species.IsConiferous)
                {
                    laiConifer += lai;
                }
                else
                {
                    laiBroadleaf += lai;
                }
                laiWeightedCanopyConductance += ruSpecies.Species.MaxCanopyConductance * lai; // weigh with LAI
            }
            float totalLai = laiBroadleaf + laiConifer;

            // handle cases with LAI < 1 (use generic "ground cover characteristics" instead)
            /* The LAI used here is derived from the "stockable" area (and not the stocked area).
               If the stand has gaps, the available trees are "thinned" across the whole area. Otherwise (when stocked area is used)
               the LAI would overestimate the transpiring canopy. However, the current solution overestimates e.g. the interception.
               If the "thinned out" LAI is below one, the rest (i.e. the gaps) are thought to be covered by ground vegetation.
            */
            if (totalLai < 1.0F)
            {
                const float groundVegetationCC = 0.02F;
                laiWeightedCanopyConductance += groundVegetationCC * (1.0F - totalLai);
                totalLai = 1.0F;
            }
            laiWeightedCanopyConductance /= totalLai;

            if (totalLai < projectFile.Model.Ecosystem.LaiThresholdForConstantStandConductance)
            {
                // following Landsberg and Waring: when LAI is < 3 (default for laiThresholdForClosedStands) a linear ramp from 0 to 3 is assumed
                // http://iland-model.org/water+cycle#transpiration_and_canopy_conductance
                laiWeightedCanopyConductance *= totalLai / projectFile.Model.Ecosystem.LaiThresholdForConstantStandConductance;
            }

            return (laiConifer, laiBroadleaf, laiWeightedCanopyConductance);
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
            if (this.soilWaterRetention == null)
            {
                throw new NotSupportedException(nameof(this.Setup) + "() must be called before calling " + nameof(this.RunYear) + "().");
            }

            WaterCycleData hydrologicState = new();

            // preparations (once a year)
            // fetch canopy characteristics from iLand, including LAI weighted average for canopy conductance
            (float laiNeedle, float laiBroadleaved, this.CanopyConductance) = this.OnStartYear(projectFile);
            this.Canopy.OnStartYear(laiNeedle, laiBroadleaved, this.CanopyConductance);

            // main loop over all days of the year
            this.SnowDayRadiation = 0.0F;
            this.SnowDays = 0;
            this.TotalEvapotranspiration = 0.0F;
            this.TotalRunoff = 0.0F;
            float daysInTimestep = 1.0F;
            WeatherTimeSeries weatherTimeSeries = this.ru.Weather.TimeSeries;
            bool isLeapYear = weatherTimeSeries.IsCurrentlyLeapYear();
            for (int weatherTimestepIndex = weatherTimeSeries.CurrentYearStartIndex, timestepInYearIndex = 0; weatherTimestepIndex < weatherTimeSeries.NextYearStartIndex; ++weatherTimestepIndex, ++timestepInYearIndex)
            {
                int dayOfYearIndex = timestepInYearIndex;
                if (weatherTimeSeries.Timestep == Timestep.Monthly)
                {
                    dayOfYearIndex = DateTimeExtensions.GetMidmonthDayIndex(timestepInYearIndex, isLeapYear);
                    daysInTimestep = DateTimeExtensions.GetDaysInMonth(timestepInYearIndex, isLeapYear);
                }

                // interception by the crown
                float throughfallInMM = this.Canopy.FlowPrecipitationTimestep(weatherTimeSeries.PrecipitationTotalInMM[weatherTimestepIndex], daysInTimestep);
                // storage in the snow pack
                // TODO: make daily mean temperature available here rather than relying on daytime mean temperature
                float infiltrationInMM = this.snowPack.FlowPrecipitationTimestep(throughfallInMM, weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex]);
                // save extra data (used by e.g. fire module)
                hydrologicState.WaterReachingSoilByWeatherTimestep[timestepInYearIndex] = infiltrationInMM;
                hydrologicState.SnowCover[timestepInYearIndex] = this.snowPack.WaterEquivalentInMM;
                if (this.snowPack.WaterEquivalentInMM > 0.0)
                {
                    this.SnowDayRadiation += weatherTimeSeries.SolarRadiationTotal[weatherTimestepIndex];
                    ++this.SnowDays;
                }

                // fill soil to capacity under stream assumption with any excess becoming runoff (percolation or overland) which disappears
                // from the model
                // This is problematic: 
                // - the steeper the terrain and the deeper the soils the more likely percolation transports significant soil moisture from
                //   resource unit with low topographic wetness indices to ones with high wetness indices
                // - soils need not be saturated for percolation to occur, so runoff here is either overland excess flow (which is uncommon)
                //   or a likely overestimate of the resource unit's net contribution to streamflow (which may be zero in upland units lacking
                //   stream channels)
                // - dropping runoff from the model is guaranteed to be structurally correct only for single resource unit models and, for
                //   any large model, likely violates conservation of mass
                // - the longer the weather timestep, the more evapotranspiration which occurs and the more likely it is runoff and soil water
                //   potential will be overestimated
                this.CurrentSoilWater += infiltrationInMM;
                if (this.CurrentSoilWater > this.FieldCapacity)
                {
                    // excess water runoff
                    float runoffInMM = this.CurrentSoilWater - this.FieldCapacity;
                    this.TotalRunoff += runoffInMM;
                    this.CurrentSoilWater = this.FieldCapacity;
                }

                float currentPsi = this.soilWaterRetention.GetSoilWaterPotentialFromWater(this.CurrentSoilWater);
                this.SoilWaterPotentialByWeatherTimestep[timestepInYearIndex] = currentPsi;

                // transpiration of the vegetation and of water intercepted in canopy
                // implicit assumption: water does not remain in canopy between days
                // calculate the LAI-weighted response values for soil water and vpd:
                float interceptionBeforeTranspiration = this.Canopy.TotalInterceptedWaterInMM;
                float soilAtmosphereResponse = this.GetSoilAtmosphereModifier(currentPsi, weatherTimeSeries.VpdMeanInKPa[weatherTimestepIndex]);
                float dayLengthInHours = this.ru.Weather.Sun.GetDayLengthInHours(dayOfYearIndex);
                float evapotranspirationInMM = this.Canopy.FlowDayEvapotranspiration3PG(projectFile, (WeatherTimeSeriesDaily)weatherTimeSeries, weatherTimestepIndex, dayLengthInHours, soilAtmosphereResponse);
                // if there is some flow from intercepted water to the ground -> add to "water_to_the_ground"
                if (this.Canopy.TotalInterceptedWaterInMM < interceptionBeforeTranspiration)
                {
                    // for now, stemflow remains liquid and does not freeze to trees or within any snowpack which might be present
                    float stemflow = interceptionBeforeTranspiration - this.Canopy.TotalInterceptedWaterInMM;
                    hydrologicState.WaterReachingSoilByWeatherTimestep[timestepInYearIndex] += stemflow;
                }

                this.CurrentSoilWater -= evapotranspirationInMM; // reduce content (transpiration)
                // add intercepted water (that is *not* evaporated) again to the soil (or add to snow if temp too low -> call to snowpack)
                this.CurrentSoilWater += this.snowPack.AddSnowWaterEquivalent(this.Canopy.TotalInterceptedWaterInMM, weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex]);

                // do not evapotranspirate water below the permanent wilt potential (approximated elsewhere as a fixed, species independent potential)
                // This is not entirely correct as evaporation, especially from upper soil layers, isn't subject to roots' water extraction
                // limits and can approach oven dryness given sufficiently hot surface temperatures.
                if (this.CurrentSoilWater < this.residualSoilWater)
                {
                    evapotranspirationInMM -= this.residualSoilWater - this.CurrentSoilWater;
                    this.CurrentSoilWater = this.residualSoilWater;
                }

                this.TotalEvapotranspiration += evapotranspirationInMM;
            }

            return hydrologicState;
        }
    }
}