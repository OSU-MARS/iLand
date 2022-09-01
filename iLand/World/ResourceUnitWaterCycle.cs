using iLand.Extensions;
using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Input.Weather;
using iLand.Tool;
using iLand.Tree;
using System;
using System.Diagnostics;

namespace iLand.World
{
    /** simulates the water cycle on a ResourceUnit.
        The WaterCycle is simulated with a daily time step on the spatial level of a ResourceUnit. Related are
        the snow module (SnowPack), and Canopy module that simulates the interception (and evaporation) of precipitation and the
        transpiration from the canopy.
        The WaterCycle covers the "soil water bucket". Main entry function is run().

        See http://iland-model.org/water+cycle
        */
    public class ResourceUnitWaterCycle
    {
        private float currentSoilWaterInMM; // current soil water content, mm water column
        private float residualSoilWater; // bucket "height" of PWP (is fixed to -4MPa) (mm)
        private readonly ResourceUnit resourceUnit; // resource unit to which this watercycle is connected
        private SoilWaterRetention? soilWaterRetention;
        private readonly SnowPack snowPack; // object representing the snow cover (aggregation, melting)

        public Canopy Canopy { get; private init; } // object representing the forest canopy (interception, evaporation)
        // TODO: should conductance move to Canopy class?
        public float CanopyConductance { get; private set; } // current canopy conductance (LAI weighted CC of available tree species), m/s

        public float FieldCapacityInMM { get; private set; } //  bucket height of field-capacity (-15 kPa, 0 kPa, mm

        public float[] EvapotranspirationInMMByMonth { get; private set; } // annual sum of evapotranspiration, mm
        // amount of water which actually reaches the ground (i.e., after interception), mm
        public float[] InfiltrationInMMByMonth { get; private init; }
        // water leaving resource unit via lateral outflow or groundwater flow due to sols reaching saturation, mm
        public float[] RunoffInMMByMonth { get; private init; }

        // public float[] SnowCoverByWeatherTimestepInYear { get; private init; } // depth of snow cover, mm water column
        // public float SnowDayRadiation { get; private set; } // sum of radiation input, MJ/m², for days with snow cover (used in albedo calculations)
        // public float SnowDays { get; private set; } // number of days with snowcover > 0

        public float[] SoilWaterPotentialByWeatherTimestepInYear { get; private init; } // soil water potential for each day of year (daily weather) or month of year (monthly weather) in kPa, needed for soil water modifier

        public ResourceUnitWaterCycle(Project projectFile, ResourceUnit resourceUnit, int weatherTimestepsPerYear)
        {
            this.residualSoilWater = Single.NaN;
            this.resourceUnit = resourceUnit;
            this.soilWaterRetention = null;
            this.snowPack = new();

            this.Canopy = new(projectFile.Model.Ecosystem.AirDensity);

            this.EvapotranspirationInMMByMonth = new float[Constant.Time.MonthsInYear];
            this.FieldCapacityInMM = Single.NaN;
            this.InfiltrationInMMByMonth = new float[Constant.Time.MonthsInYear];
            this.RunoffInMMByMonth = new float[Constant.Time.MonthsInYear];

            // this.SnowCoverByWeatherTimestepInYear = new float[weatherTimestepsPerYear];
            // this.SnowDayRadiation = Single.NaN;
            // this.SnowDays = Single.NaN;

            this.SoilWaterPotentialByWeatherTimestepInYear = new float[weatherTimestepsPerYear];
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
            this.FieldCapacityInMM = this.soilWaterRetention.GetSoilWaterFromPotential(psiSaturation);
            this.residualSoilWater = this.soilWaterRetention.GetSoilWaterFromPotential(projectFile.Model.Settings.SoilPermanentWiltPotentialInKPA);

            if (this.FieldCapacityInMM <= this.residualSoilWater)
            {
                throw new NotSupportedException("Field capacity is at or below permanent wilting point. This indicates an internal error in the soil water retention curve or, if the soil saturation potential is specified in the project file (model.settings.soilSaturationPotential), that the saturation potential is too negative.");
            }

            // start with full soil water content (in the middle of winter)
            // BUGBUG: depends on whether soils on site ever reach saturation and, if so, whether simulation starts at a time when soils are saturated
            this.currentSoilWaterInMM = this.FieldCapacityInMM;
            //if (model.Files.LogDebug())
            //{
            //    Debug.WriteLine("setup of water: Psi_sat (kPa) " + mPsi_sat + " Theta_sat " + mTheta_sat + " coeff. b " + mPsi_koeff_b);
            //}

            this.Canopy.BroadleafStorageInMM = projectFile.Model.Ecosystem.InterceptionStorageBroadleaf;
            this.Canopy.NeedleStorageInMM = projectFile.Model.Ecosystem.InterceptionStorageNeedle;
            this.CanopyConductance = 0.0F;

            // this.EvapotranspirationInMMByMonth is zeroed at start of year
            // this.InfiltrationInMMByMonth is zeroed at start of year
            // this.RunoffInMMByMonth is zeroed at start of year
            // this.SoilWaterPotentialByWeatherTimestepInYear is assigned timestep by timestep

            // this.SnowDays = 0;
            // this.SnowDayRadiation = 0.0F;
            this.snowPack.MeltTemperatureInC = projectFile.Model.Ecosystem.SnowmeltTemperature;
        }

        // get canopy characteristics of the resource unit.
        // It is important species statistics are valid when this function is called (LAI)!
        private (float laiConifer, float laiBroadleaf, float laiWeightedCanopyConductance) OnStartYear(Project projectFile)
        {
            float laiConifer = 0.0F;
            float laiBroadleaf = 0.0F;
            float laiWeightedCanopyConductance = 0.0F;
            foreach (ResourceUnitTreeSpecies ruSpecies in this.resourceUnit.Trees.SpeciesAvailableOnResourceUnit) 
            {
                float lai = ruSpecies.StatisticsLive.LeafAreaIndex; // use previous year's LAI as this year's hasn't yet been computed
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

        /// Calculate combined VPD and soil water response for all species on the RU. This is used for calculation of the transpiration.
        private float GetSoilAtmosphereModifier(float psiInKilopascals, float vpdInKilopascals)
        {
            float soilAtmosphereModifier = 0.0F; // LAI weighted minimum modifier for all species on the RU
            float totalLaiFactor = 0.0F;
            foreach (ResourceUnitTreeSpecies ruSpecies in this.resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
            {
                if (ruSpecies.LaiFraction > 0.0F)
                {
                    // retrieve the minimum of VPD and soil water modifier for species
                    float limitingModifier = ruSpecies.TreeGrowth.Modifiers.GetMostLimitingSoilWaterOrVpdModifier(psiInKilopascals, vpdInKilopascals);
                    soilAtmosphereModifier += limitingModifier * ruSpecies.LaiFraction;
                    totalLaiFactor += ruSpecies.LaiFraction;
                }
            }

            if (totalLaiFactor < 1.0F)
            {
                // the LAI is below 1: the rest is considered as "ground vegetation"
                soilAtmosphereModifier += ResourceUnitWaterCycle.GetUnderstoryLimitingWaterVpdModifier(psiInKilopascals, vpdInKilopascals) * (1.0F - totalLaiFactor);
            }

            // add an aging factor to the total growth modifier (averageAging: leaf area weighted mean aging value):
            // conceptually: totalModifier = agingModifier * min(vpdModifier, waterModifier)
            if (totalLaiFactor == 1.0F)
            {
                soilAtmosphereModifier *= resourceUnit.Trees.AverageLeafAreaWeightedAgingFactor; // no ground cover: use aging value for all LA
            }
            else if (totalLaiFactor > 0.0F && resourceUnit.Trees.AverageLeafAreaWeightedAgingFactor > 0.0F)
            {
                soilAtmosphereModifier *= (1.0F - totalLaiFactor) * 1.0F + (totalLaiFactor * resourceUnit.Trees.AverageLeafAreaWeightedAgingFactor); // between 0..1: a part of the LAI is "ground cover" (aging=1)
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
        private static float GetUnderstoryLimitingWaterVpdModifier(float psiInKilopascals, float vpdInKilopascals)
        {
            // constant parameters used for ground vegetation:
            // see TreeSpecies.GetSoilWaterModifier() and ResourceUnitTreeSpeciesResponse.GetLimitingSoilWaterOrVpdModifier()
            float psiMPa = 0.001F * psiInKilopascals; // convert to MPa
            float waterModifier = Maths.Limit(1.0F - psiMPa / 1.5F, 0.0F, 1.0F);
            // see Species.GetVpdModifier()

            float vpdModifier = MathF.Exp(-0.6F * vpdInKilopascals);
            return MathF.Min(waterModifier, vpdModifier);
        }

        /// Main Water Cycle function. This function triggers all water related tasks for
        /// one simulation year.
        /// @sa http://iland-model.org/water+cycle
        public void RunYear(Project projectFile)
        {
            if (this.soilWaterRetention == null)
            {
                throw new NotSupportedException(nameof(this.Setup) + "() must be called before calling " + nameof(this.RunYear) + "().");
            }

            // preparations (once a year)
            // fetch canopy characteristics from iLand, including LAI weighted average for canopy conductance
            (float laiNeedle, float laiBroadleaved, this.CanopyConductance) = this.OnStartYear(projectFile);
            this.Canopy.OnStartYear(laiNeedle, laiBroadleaved, this.CanopyConductance);

            // main loop over all days of the year
            Array.Fill(this.EvapotranspirationInMMByMonth, 0.0F);
            Array.Fill(this.InfiltrationInMMByMonth, 0.0F);
            Array.Fill(this.RunoffInMMByMonth, 0.0F);
            // this.SnowDayRadiation = 0.0F;
            // this.SnowDays = 0;

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
                float infiltrationInMM = this.snowPack.FlowPrecipitationTimestep(throughfallInMM, weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex]);
                // if there is some flow from intercepted water to the ground -> add to "water_to_the_ground"
                float interceptionBeforeTranspiration = this.Canopy.TotalInterceptedWaterInMM;
                if (this.Canopy.TotalInterceptedWaterInMM < interceptionBeforeTranspiration)
                {
                    // for now, stemflow remains liquid and does not freeze to trees or within any snowpack which might be present
                    float stemflowInMM = interceptionBeforeTranspiration - this.Canopy.TotalInterceptedWaterInMM;
                    infiltrationInMM += stemflowInMM;
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
                this.currentSoilWaterInMM += infiltrationInMM;
                if (this.currentSoilWaterInMM > this.FieldCapacityInMM)
                {
                    // excess water runoff
                    float runoffInMM = this.currentSoilWaterInMM - this.FieldCapacityInMM;
                    this.currentSoilWaterInMM = this.FieldCapacityInMM;

                    this.RunoffInMMByMonth[monthOfYearIndex] += runoffInMM;
                }

                float currentPsi = this.soilWaterRetention.GetSoilWaterPotentialFromWater(this.currentSoilWaterInMM);
                this.SoilWaterPotentialByWeatherTimestepInYear[weatherTimestepInYearIndex] = currentPsi;

                // transpiration of the vegetation and of water intercepted in canopy
                // implicit assumption: water does not remain in canopy between weather timesteps, even for daily time series
                // calculate the LAI-weighted growth modifiers for soil water and VPD
                float soilAtmosphereModifier = this.GetSoilAtmosphereModifier(currentPsi, weatherTimeSeries.VpdMeanInKPa[weatherTimestepIndex]);
                float dayLengthInHours = this.resourceUnit.Weather.Sun.GetDayLengthInHours(dayOfYearIndex);
                float evapotranspirationInMM = this.Canopy.FlowEvapotranspirationTimestep3PG(projectFile, weatherTimeSeries, weatherTimestepIndex, dayLengthInHours, soilAtmosphereModifier);

                this.currentSoilWaterInMM -= evapotranspirationInMM; // reduce content (transpiration)
                // add intercepted water (that is *not* evaporated) again to the soil (or add to snow if temp too low -> call to snowpack)
                this.currentSoilWaterInMM += this.snowPack.AddSnowWaterEquivalent(this.Canopy.TotalInterceptedWaterInMM, weatherTimeSeries.TemperatureDaytimeMean[weatherTimestepIndex]);

                // do not evapotranspirate water below the permanent wilt potential (approximated elsewhere as a fixed, species independent potential)
                // This is not entirely correct as evaporation, especially from upper soil layers, isn't subject to roots' water extraction
                // limits and can approach oven dryness given sufficiently hot surface temperatures.
                if (this.currentSoilWaterInMM < this.residualSoilWater)
                {
                    evapotranspirationInMM -= this.residualSoilWater - this.currentSoilWaterInMM;
                    this.currentSoilWaterInMM = this.residualSoilWater;
                }

                // save extra data used by logging or modules (e.g. fire)
                // this.SnowCoverByWeatherTimestepInYear[weatherTimestepInYearIndex] = this.snowPack.WaterEquivalentInMM;
                //if (this.snowPack.WaterEquivalentInMM > 0.0)
                //{
                //    this.SnowDayRadiation += weatherTimeSeries.SolarRadiationTotal[weatherTimestepIndex];
                //    ++this.SnowDays;
                //}

                this.EvapotranspirationInMMByMonth[monthOfYearIndex] += evapotranspirationInMM;
                this.InfiltrationInMMByMonth[monthOfYearIndex] += infiltrationInMM;
            }
        }
    }
}