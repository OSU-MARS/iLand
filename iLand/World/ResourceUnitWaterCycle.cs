// C++/core/watercycle.h
using iLand.Extensions;
using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Input.Weather;
using iLand.Tool;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using LeafPhenology = iLand.Tree.LeafPhenology;

namespace iLand.World
{
    /** simulates the water cycle on a ResourceUnit.
        The WaterCycle is simulated with a daily time step on the spatial level of a ResourceUnit. Related are
        the snow module (SnowPack), and Canopy module that simulates the interception (and evaporation) of precipitation and the
        transpiration from the canopy.
        The WaterCycle covers the "soil water bucket". Main entry function is run().

        See https://iland-model.org/water+cycle
        */
    public class ResourceUnitWaterCycle
    {
        private float residualSoilWater; // bucket "height" of PWP (is fixed to -4MPa) (mm)
        private readonly ResourceUnit resourceUnit; // resource unit to which this watercycle is connected

        // ground vegetation
        private float groundVegetationLeafAreaIndex; ///< LAI of the ground vegetation (parameter)
        private float groundVegetationPsiMin; ///< Psi Min (MPa) that is assumed for ground vegetation (parameter)

        /// container for storing min-psi values per resource unit + phenology class
        /// key: phenology group, value: psiMin (2 week minimum) MPa
        private readonly Dictionary<int, float> establishmentMinPsiByPhenologyGroup; // TODO: change to array

        public Canopy Canopy { get; private init; } // object representing the forest canopy (interception, evaporation)
        // TODO: should conductance move to Canopy class?
        public float CanopyConductance { get; private set; } // current canopy conductance (LAI weighted CC of available tree species), m/s

        public float CurrentSoilWaterInMM { get; private set; } // current soil water content, mm water column (C++: currentContent(), mContent)
        public float EffectiveLeafAreaIndex { get; private set; } ///< effective LAI for transpiration: includes ground vegetation, saplings and adult trees (C++: mEffectiveLAI)
        public float FieldCapacityInMM { get; private set; } //  bucket height of field-capacity (-15 kPa, 0 kPa, mm
        public float MeanSoilWaterContentAnnualInMM { get; private set; } ///< mean of annual soil water content (mm) (C++: mMeanSoilWaterContent)
        public float MeanSoilWaterContentGrowingSeasonInMM { get; private set; } ///< mean soil water content (mm) during the growing season (fixed: April - September) (C++ mMeanGrowingSeasonSWC)

        public float[] EvapotranspirationInMMByMonth { get; private set; } // annual sum of evapotranspiration, mm
        // amount of water which actually reaches the ground (i.e., after interception), mm
        public float[] InfiltrationInMMByMonth { get; private init; }
        // object representing permafrost soil conditions
        public Permafrost? Permafrost { get; private init; }
        // water leaving resource unit via lateral outflow or groundwater flow due to sols reaching saturation, mm
        public float[] RunoffInMMByMonth { get; private init; }

        // public float[] SnowCoverByWeatherTimestepInYear { get; private init; } // depth of snow cover, mm water column
        public float SnowDayRadiation { get; private set; } // sum of radiation input, MJ/m², for days with snow cover (used in albedo calculations)
        public float SnowDays { get; private set; } // number of days with snowcover > 0
        public Snowpack Snowpack { get; private init; } // object representing the snow cover (aggregation, melting)

        public float[] SoilWaterPotentialByWeatherTimestepInYear { get; private init; } // soil water potential for each day of year (daily weather) or month of year (monthly weather) in kPa, needed for soil water modifier
        public SoilWaterRetention? SoilWaterRetention { get; private set; }

        public ResourceUnitWaterCycle(Project project, ResourceUnit resourceUnit, int weatherTimestepsPerYear)
        {
            this.establishmentMinPsiByPhenologyGroup = [];
            this.Permafrost = null;
            this.residualSoilWater = Single.NaN;
            this.resourceUnit = resourceUnit;

            this.Canopy = new(project.Model.Ecosystem.AirDensity);
            this.EvapotranspirationInMMByMonth = new float[Constant.Time.MonthsInYear];
            this.FieldCapacityInMM = Single.NaN;
            this.InfiltrationInMMByMonth = new float[Constant.Time.MonthsInYear];
            this.RunoffInMMByMonth = new float[Constant.Time.MonthsInYear];
            this.SnowDayRadiation = Single.NaN;
            this.SnowDays = Single.NaN;
            this.SoilWaterRetention = null;
            this.Snowpack = new(project);

            if (project.Model.Permafrost.Enabled)
            {
                this.Permafrost = new(project.Model.Permafrost, resourceUnit);
            }

            // this.SnowCoverByWeatherTimestepInYear = new float[weatherTimestepsPerYear];
            this.SoilWaterPotentialByWeatherTimestepInYear = new float[weatherTimestepsPerYear];
        }

        public void SetActiveLayerDepth(float activeLayerDepthInMM)
        {
            Debug.Assert(this.SoilWaterRetention != null && (0.0F <= activeLayerDepthInMM) && (activeLayerDepthInMM <= this.SoilWaterRetention.SoilPlantAccessibleDepthInMM));
            this.SoilWaterRetention.SetActiveLayerDepth(activeLayerDepthInMM);

            float fractionThawed = activeLayerDepthInMM / this.SoilWaterRetention.SoilPlantAccessibleDepthInMM;
            this.CurrentSoilWaterInMM *= fractionThawed;
            this.FieldCapacityInMM = this.SoilWaterRetention.GetSoilWaterFromPotential(this.SoilWaterRetention.SaturationPotentialInKPa);
        }

        public void Setup(Project project, ResourceUnitEnvironment environment)
        {
            // soil water
            this.SoilWaterRetention = SoilWaterRetention.Create(environment, project.Model.Settings.SoilSaturationPotentialInKPa);
            this.FieldCapacityInMM = this.SoilWaterRetention.GetSoilWaterFromPotential(this.SoilWaterRetention.SaturationPotentialInKPa);
            this.residualSoilWater = this.SoilWaterRetention.GetSoilWaterFromPotential(project.Model.Settings.SoilPermanentWiltPotentialInKPA);

            if (this.FieldCapacityInMM <= this.residualSoilWater)
            {
                throw new NotSupportedException("Field capacity is at or below permanent wilting point. This indicates an internal error in the soil water retention curve or, if the soil saturation potential is specified in the project file (model.settings.soilSaturationPotential), that the saturation potential is too negative.");
            }

            // start with full soil water content (in the middle of winter)
            // BUGBUG: depends on whether soils on site ever reach saturation and, if so, whether simulation starts at a time when soils are saturated
            this.CurrentSoilWaterInMM = this.FieldCapacityInMM;
            //if (model.Files.LogDebug())
            //{
            //    Debug.WriteLine("setup of water: Psi_sat (kPa) " + mPsi_sat + " Theta_sat " + mTheta_sat + " coeff. b " + mPsi_koeff_b);
            //}

            this.Canopy.BroadleafStorageInMM = project.Model.Ecosystem.InterceptionStorageBroadleafInMM;
            this.Canopy.NeedleStorageInMM = project.Model.Ecosystem.InterceptionStorageNeedleInMM;
            this.CanopyConductance = 0.0F;

            // this.EvapotranspirationInMMByMonth is zeroed at start of year
            // this.InfiltrationInMMByMonth is zeroed at start of year
            // this.RunoffInMMByMonth is zeroed at start of year
            // this.SoilWaterPotentialByWeatherTimestepInYear is assigned timestep by timestep

            // snow settings
            this.SnowDays = 0;
            this.SnowDayRadiation = 0.0F;

            // ground vegetation: variable LAI and Psi_min
            this.groundVegetationLeafAreaIndex = project.Model.Ecosystem.GroundVegetationLeafAreaIndex;
            this.groundVegetationPsiMin = project.Model.Ecosystem.GroundVegetationPsiMin;

            this.Permafrost?.Setup(environment);
        }

        /// <summary>
        /// get canopy characteristics of the resource unit 
        /// </summary>
        /// <remarks>
        /// C++: WaterCycle::getStandValues()
        /// It is important species statistics are valid when this function is called (LAI)!
        /// </remarks>
        private (float laiConifer, float laiBroadleaf, float laiWeightedCanopyConductance) OnStartYear(Project projectFile, RUSpeciesShares species_shares)
        {
            float laiConifer = 0.0F;
            float laiBroadleaf = 0.0F;
            float laiWeightedCanopyConductance = 0.0F;
            for (int speciesIndex = 0; speciesIndex < this.resourceUnit.Trees.SpeciesAvailableOnResourceUnit.Count; ++speciesIndex) 
            {
                ResourceUnitTreeSpecies ruSpecies = this.resourceUnit.Trees.SpeciesAvailableOnResourceUnit[speciesIndex];
                float lai = ruSpecies.StatisticsLive.LeafAreaIndex + ruSpecies.SaplingStats.LeafAreaIndex; // use previous year's LAI as this year's hasn't yet been computed
                species_shares.lai_share[speciesIndex] = lai;
                if (ruSpecies.Species.IsConiferous)
                {
                    laiConifer += lai;
                }
                else
                {
                    laiBroadleaf += lai;
                }
                laiWeightedCanopyConductance += ruSpecies.Species.MaxCanopyConductance * lai; // weight with LAI
            }
            float totalLai = laiBroadleaf + laiConifer;

            // handle cases with LAI < 1 (use generic "ground cover characteristics" instead)
            /* The LAI used here is derived from the "stockable" area (and not the stocked area).
               If the stand has gaps, the available trees are "thinned" across the whole area. Otherwise (when stocked area is used)
               the LAI would overestimate the transpiring canopy. However, the current solution overestimates e.g. the interception.
               If the "thinned out" LAI is below one, the rest (i.e. the gaps) are thought to be covered by ground vegetation.
            */
            if (totalLai < this.groundVegetationLeafAreaIndex)
            {
                const float groundVegetationCC = 0.02F;
                laiWeightedCanopyConductance += groundVegetationCC * (1.0F - totalLai);
                species_shares.ground_vegetation_share = (this.groundVegetationLeafAreaIndex - totalLai) / this.groundVegetationLeafAreaIndex;
                totalLai = 1.0F;
            }

            this.EffectiveLeafAreaIndex = totalLai;
            species_shares.total_lai = totalLai;
            if (totalLai > 0.0F)
            {
                laiWeightedCanopyConductance /= totalLai;
                species_shares.adult_trees_share = this.resourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.LeafAreaIndex / totalLai; // trees >4m
                for (int speciesIndex = 0; speciesIndex < species_shares.lai_share.Length; ++speciesIndex)
                {
                    species_shares.lai_share[speciesIndex] /= totalLai;
                }
            }
            else
            {
                Debug.Assert(laiWeightedCanopyConductance == 0.0F);
            }

            if (totalLai < projectFile.Model.Ecosystem.LaiThresholdForConstantStandConductance)
            {
                // following Landsberg and Waring: when LAI is < 3 (default for laiThresholdForClosedStands) a linear ramp from 0 to 3 is assumed
                // https://iland-model.org/water+cycle#transpiration_and_canopy_conductance
                laiWeightedCanopyConductance *= totalLai / projectFile.Model.Ecosystem.LaiThresholdForConstantStandConductance;
            }

            return (laiConifer, laiBroadleaf, laiWeightedCanopyConductance);
        }

        /// Calculate combined VPD and soil water response for all species on the RU. This is used for calculation of the transpiration.
        private float GetSoilAtmosphereModifier(RUSpeciesShares species_share, float psiInKilopascals, float vpdInKilopascals) // C++: WaterCycle::calculateSoilAtmosphereResponse()
        {
            // the species_share has pre-calculated shares for the species (and ground-veg) on the total LAI
            // that effectively evapotranspirates water.
            // sum( species_share.lai_share ) + species_share.ground_vegetation_share = 1
            float soilAtmosphereModifier = 0.0F; // LAI weighted minimum modifier for all species on the RU
            float totalLaiFactor = 0.0F;
            for (int speciesIndex = 0; speciesIndex < species_share.lai_share.Length; ++speciesIndex)
            {
                float laiShare = species_share.lai_share[speciesIndex];
                if (laiShare > 0.0F)
                {
                    // retrieve the minimum of VPD and soil water modifier for species
                    ResourceUnitTreeSpecies ruSpecies = this.resourceUnit.Trees.SpeciesAvailableOnResourceUnit[speciesIndex];
                    float limitingModifier = ruSpecies.TreeGrowth.Modifiers.GetMostLimitingSoilWaterOrVpdModifier(psiInKilopascals, vpdInKilopascals);
                    soilAtmosphereModifier += limitingModifier * laiShare;
                    totalLaiFactor += laiShare;
                }
            }

            // add ground vegetation (only effective if the total LAI is below a threshold)
            if (species_share.ground_vegetation_share > 0.0F)
            {
                // the LAI is below the threshold (default=1): the rest is considered as "ground vegetation": VPD-exponent is a constant
                float ground_response = ResourceUnitWaterCycle.GetUnderstoryLimitingWaterVpdModifier(psiInKilopascals, vpdInKilopascals, this.groundVegetationPsiMin, -0.6F);
                soilAtmosphereModifier += ground_response * species_share.ground_vegetation_share;
            }

            // add an aging factor to the total growth modifier (averageAging: leaf area weighted mean aging value):
            // conceptually: totalModifier = agingModifier * min(vpdModifier, waterModifier)
            // apply the aging only for the part of the LAI from adult trees; assume no aging (=1) for saplings/ground vegetation
            if (species_share.adult_trees_share > 0.0F)
            {
                float aging_factor = resourceUnit.Trees.AverageLeafAreaWeightedAgingFactor * species_share.adult_trees_share + 1.0F - species_share.adult_trees_share;
                soilAtmosphereModifier *= aging_factor;
            }

            if (this.resourceUnit.Trees.AverageLeafAreaWeightedAgingFactor > 1.0F || this.resourceUnit.Trees.AverageLeafAreaWeightedAgingFactor < 0.0F || soilAtmosphereModifier < 0.0F || soilAtmosphereModifier > 1.0F)
            {
                throw new NotSupportedException("Average aging or soil atmosphere modifier invalid. Aging: " + resourceUnit.Trees.AverageLeafAreaWeightedAgingFactor + ", soil-atmosphere response " + soilAtmosphereModifier + ", total LAI factor: " + totalLaiFactor + ".");
            }
            return soilAtmosphereModifier;
        }

        /// calculate responses for ground vegetation, i.e. for "unstocked" areas.
        /// Same as tree species calculations but different limiting matric potential.
        /// @return Minimum of vpd and soil water modifer
        private static float GetUnderstoryLimitingWaterVpdModifier(float psiInKilopascals, float vpdInKilopascals, float psi_min, float vpd_exp) // C++: calculateBaseSoilAtmosphereResponse()
        {
            // constant parameters used for ground vegetation:
            // see TreeSpecies.GetSoilWaterModifier() and ResourceUnitTreeSpeciesResponse.GetLimitingSoilWaterOrVpdModifier()
            float psiMPa = 0.001F * psiInKilopascals; // convert to MPa
            float waterModifier = Maths.Limit((psiMPa - psi_min) / (-0.01F - psi_min), 0.0F, 1.0F);
            // see Species.GetVpdModifier() (C++ species::vpdResponse())

            float vpdModifier = MathF.Exp(vpd_exp * vpdInKilopascals);
            return MathF.Min(waterModifier, vpdModifier);
        }

        /// Main Water Cycle function. This function triggers all water related tasks for one simulation year.
        /// @sa https://iland-model.org/water+cycle
        public void RunYear(Project project) // C++: WaterCycle::run()
        {
            if (this.SoilWaterRetention == null)
            {
                throw new NotSupportedException(nameof(this.Setup) + "() must be called before calling " + nameof(this.RunYear) + "().");
            }

            // preparations (once a year)
            // fetch canopy characteristics from iLand, including LAI weighted average for canopy conductance
            RUSpeciesShares species_share = new(this.resourceUnit.Trees.SpeciesAvailableOnResourceUnit.Count); // TODO: don't realloc
            (float laiNeedle, float laiBroadleaved, this.CanopyConductance) = this.OnStartYear(project, species_share);
            this.Canopy.OnStartYear(laiNeedle, laiBroadleaved, this.CanopyConductance);
            this.Permafrost?.OnNewYear();

            // main loop over all days of the year
            Array.Fill(this.EvapotranspirationInMMByMonth, 0.0F);
            Array.Fill(this.InfiltrationInMMByMonth, 0.0F);
            Array.Fill(this.RunoffInMMByMonth, 0.0F);
            this.SnowDayRadiation = 0.0F;
            this.SnowDays = 0;
            int growing_season_days = 0;
            this.MeanSoilWaterContentGrowingSeasonInMM = 0.0F;
            this.MeanSoilWaterContentAnnualInMM = 0.0F;

            float daysInTimestep = 1.0F;
            WeatherTimeSeries weatherTimeSeries = this.resourceUnit.Weather.TimeSeries;
            bool isLeapYear = weatherTimeSeries.IsCurrentlyLeapYear();
            bool isMonthlyWeather = weatherTimeSeries.Timestep == Timestep.Monthly;
            Debug.Assert(isMonthlyWeather || (weatherTimeSeries.Timestep == Timestep.Daily));
            for (int weatherTimestepIndex = weatherTimeSeries.CurrentYearStartIndex, weatherTimestepInYearIndex = 0; weatherTimestepIndex < weatherTimeSeries.NextYearStartIndex; ++weatherTimestepIndex, ++weatherTimestepInYearIndex)
            {
                int dayOfYearIndex;
                int monthOfYearIndex;
                if (isMonthlyWeather)
                {
                    dayOfYearIndex = DateTimeExtensions.GetMidmonthDayIndex(weatherTimestepInYearIndex, isLeapYear);
                    daysInTimestep = (float)DateTimeExtensions.GetDaysInMonth(weatherTimestepInYearIndex, isLeapYear);
                    monthOfYearIndex = weatherTimestepInYearIndex;
                }
                else
                {
                    dayOfYearIndex = weatherTimestepInYearIndex;
                    // daysInTimestep never changes from 1.0
                    monthOfYearIndex = DateTimeExtensions.DayOfYearToMonthIndex(dayOfYearIndex, isLeapYear);
                }

                // interception by the crown
                float throughfallInMM = this.Canopy.FlowPrecipitationTimestep(weatherTimeSeries.PrecipitationTotalInMM[weatherTimestepIndex], daysInTimestep);
                // storage in the snow pack
                // TODO: make daily mean temperature available here rather than relying on daytime mean temperature?               
                float infiltrationInMM = this.Snowpack.FlowPrecipitationTimestep(throughfallInMM, weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex]);
                // if there is some flow from intercepted water to the ground -> add to "water_to_the_ground"
                float interceptionBeforeTranspiration = this.Canopy.TotalInterceptedWaterInMM;
                if (this.Canopy.TotalInterceptedWaterInMM < interceptionBeforeTranspiration)
                {
                    // for now, stemflow remains liquid and does not freeze to trees or within any snowpack which might be present
                    float stemflowInMM = interceptionBeforeTranspiration - this.Canopy.TotalInterceptedWaterInMM;
                    infiltrationInMM += stemflowInMM;
                }

                // invoke permafrost module (if active)
                this.Permafrost?.CalculateTimestepFreezeThaw(weatherTimestepIndex);

                // add rest to soil.
                // Fill soil to capacity under stream assumption with any excess becoming runoff (percolation or overland) which disappears
                // from the model. This is problematic: 
                // - the steeper the terrain and the deeper the soils the more likely percolation transports significant soil moisture from
                //   resource unit with low topographic wetness indices to ones with high wetness indices
                // - soils need not be saturated for percolation to occur, so runoff here is either overland excess flow (which is uncommon)
                //   or a likely overestimate of the resource unit's net contribution to streamflow (which may be zero in upland units lacking
                //   stream channels)
                // - dropping runoff from the model is guaranteed to be structurally correct only for single resource unit models and, for
                //   any large model, likely violates conservation of mass
                // - the longer the weather timestep, the more evapotranspiration which occurs and the more likely it is runoff and soil water
                //   potential will be overestimated
                this.CurrentSoilWaterInMM += infiltrationInMM;
                if (this.CurrentSoilWaterInMM > this.FieldCapacityInMM)
                {
                    // excess water runoff
                    float runoffInMM = this.CurrentSoilWaterInMM - this.FieldCapacityInMM;
                    this.CurrentSoilWaterInMM = this.FieldCapacityInMM;

                    this.RunoffInMMByMonth[monthOfYearIndex] += runoffInMM;
                }

                float currentPsi = this.SoilWaterRetention.GetSoilWaterPotentialFromWater(this.CurrentSoilWaterInMM);
                this.SoilWaterPotentialByWeatherTimestepInYear[weatherTimestepInYearIndex] = currentPsi;

                // transpiration of the vegetation and of water intercepted in canopy
                // implicit assumption: water does not remain in canopy between weather timesteps, even for daily time series
                // calculate the LAI-weighted growth modifiers for soil water and VPD
                float soilAtmosphereModifier = this.GetSoilAtmosphereModifier(species_share, currentPsi, weatherTimeSeries.VpdMeanInKPa[weatherTimestepIndex]);
                float dayLengthInHours = this.resourceUnit.Weather.Sun.GetDayLengthInHours(dayOfYearIndex);
                float evapotranspirationInMM = this.Canopy.FlowEvapotranspirationTimestep3PG(project, weatherTimeSeries, weatherTimestepIndex, dayLengthInHours, soilAtmosphereModifier);

                this.CurrentSoilWaterInMM -= evapotranspirationInMM; // reduce content (transpiration)
                // add intercepted water (that is *not* evaporated) again to the soil (or add to snow if temp too low -> call to snowpack)
                this.CurrentSoilWaterInMM += this.Snowpack.AddSnowWaterEquivalent(this.Canopy.TotalInterceptedWaterInMM, weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex]);

                // do not evapotranspirate water below the permanent wilt potential (approximated elsewhere as a fixed, species independent potential)
                // This is not entirely correct as evaporation, especially from upper soil layers, isn't subject to roots' water extraction
                // limits and can approach oven dryness given sufficiently hot surface temperatures.
                if (this.CurrentSoilWaterInMM < this.residualSoilWater)
                {
                    evapotranspirationInMM -= this.residualSoilWater - this.CurrentSoilWaterInMM;
                    this.CurrentSoilWaterInMM = this.residualSoilWater;
                }

                // save extra data used by logging or modules (e.g. fire)
                // this.SnowCoverByWeatherTimestepInYear[weatherTimestepInYearIndex] = this.snowPack.WaterEquivalentInMM;
                if (this.Snowpack.WaterEquivalentInMM > 0.0F)
                {
                    this.SnowDayRadiation += weatherTimeSeries.SolarRadiationTotal[weatherTimestepIndex];
                    ++this.SnowDays;
                }

                this.EvapotranspirationInMMByMonth[monthOfYearIndex] += evapotranspirationInMM;
                this.InfiltrationInMMByMonth[monthOfYearIndex] += infiltrationInMM;

                if (monthOfYearIndex > 2 && monthOfYearIndex < 9) // BUGBUG: hardcoded growing season for temperate northern latitudes
                {
                    this.MeanSoilWaterContentGrowingSeasonInMM += this.CurrentSoilWaterInMM;
                    growing_season_days++;
                }
                this.MeanSoilWaterContentAnnualInMM += this.CurrentSoilWaterInMM;
            }

            int weatherTimesteps = weatherTimeSeries.NextYearStartIndex - weatherTimeSeries.CurrentYearStartIndex;
            this.MeanSoilWaterContentAnnualInMM /= weatherTimesteps;
            this.MeanSoilWaterContentGrowingSeasonInMM /= growing_season_days;

            // call external modules
            // GlobalSettings::instance()->model()->modules()->calculateWater(mRU, &add_data);
            // mLastYear = GlobalSettings::instance()->currentYear();

            // reset deciduous litter counter
            this.resourceUnit.Snags?.ZeroDeciduousFoliage();
        }

        private void ZeroEstablishmentPsiMin()
        {
            if (this.establishmentMinPsiByPhenologyGroup.Count == 0)
            {
                for (int pg = 0; pg < this.resourceUnit.Weather.TreeSpeciesPhenology.Count; ++pg)
                {
                    this.establishmentMinPsiByPhenologyGroup[pg] = 0;
                }
            }
            else
            {
                // clear values if already populated
                foreach (int speciesIndex in this.establishmentMinPsiByPhenologyGroup.Keys)
                {
                    this.establishmentMinPsiByPhenologyGroup[speciesIndex] = Single.NaN;
                }
            }
        }

        public float GetEstablishmentPsiMin(int phenologyGroup)
        {
            // query the container and run the calculation for the current RU if value is
            // not yet calculated
            float psi = this.establishmentMinPsiByPhenologyGroup[phenologyGroup];
            if (Single.IsNaN(psi))
            {
                // note: currently no Mutex required for parallel execution (for RUs)
                this.GetMinimumMovingAveragePsi(); // calculate once per RU
                psi = this.establishmentMinPsiByPhenologyGroup[phenologyGroup];
            }
            return psi;
        }

        /// calculate the psi min over the vegetation period for all
        /// phenology types for the current resource unit (and store in a container)
        private void GetMinimumMovingAveragePsi() // C++: WaterCycle::calculatePsiMin()
        {
            // two week (14 days) running average of actual psi-values on the resource unit
            const int nwindow = 14;
            Span<float> psi_buffer = stackalloc float[nwindow];

            Weather weather = this.resourceUnit.Weather;
            WeatherTimeSeries weatherSeries = weather.TimeSeries;
            if (weatherSeries.Timestep != Timestep.Daily)
            {
                throw new NotSupportedException(); // TODO: support monthly timesteps
            }
            for (int phenologyIndex = 0; phenologyIndex < weather.TreeSpeciesPhenology.Count; ++phenologyIndex)
            {
                float psi_min = 0.0F;
                LeafPhenology pheno = weather.TreeSpeciesPhenology[phenologyIndex];
                int veg_period_start = pheno.LeafOnStartDayOfYearIndex;
                int veg_period_end = pheno.LeafOnEndDayOfYearIndex;

                psi_buffer.Clear(); // reset to zero
                float current_sum = 0.0F;

                int i_buffer = 0;
                float min_average = 9999999.0F;
                float current_avg = 0.0F;
                for (int weatherTimestepIndex = weatherSeries.CurrentYearStartIndex; weatherTimestepIndex < weatherSeries.NextYearStartIndex; ++weatherTimestepIndex) 
                {
                    // running average: remove oldest item, add new item in a ringbuffer
                    current_sum -= psi_buffer[i_buffer];
                    psi_buffer[i_buffer] = this.SoilWaterPotentialByWeatherTimestepInYear[weatherTimestepIndex];
                    current_sum += psi_buffer[i_buffer];

                    if (weatherTimestepIndex >= veg_period_start && weatherTimestepIndex <= veg_period_end) 
                    {
                        current_avg = weatherTimestepIndex > 0 ? current_sum / Int32.Min(weatherTimestepIndex, nwindow) : current_sum;
                        min_average = Single.Min(min_average, current_avg);
                    }

                    // move to next value in the buffer
                    i_buffer = (i_buffer + 1) % nwindow;
                }

                if (min_average > 1000.0F)
                {
                    psi_min = 0.0F;
                }
                else
                {
                    psi_min = min_average / 1000.0F; // MPa
                }
                this.establishmentMinPsiByPhenologyGroup[phenologyIndex] = psi_min;
            }
        }

        /// stores intermediate data: LAI shares of species (including saplings)
        /// fraction of ground vegetation
        private class RUSpeciesShares
        {
            public float[] lai_share; // for each species a share [0..1]
            public float ground_vegetation_share; // the share of ground vegetation; sum(lai_share)+ground_vegetation_share = 1
            public float adult_trees_share; // share of adult trees (>4m) on total LAI (relevant for aging)
            public float total_lai; // total effective LAI

            public RUSpeciesShares(int n_species)
            {
                this.adult_trees_share = 0.0F;
                this.ground_vegetation_share = 0.0F;
                this.lai_share = new float[n_species];
                this.total_lai = 0.0F;
            }
        }
    }
}