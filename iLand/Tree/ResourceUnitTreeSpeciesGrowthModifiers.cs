using iLand.Extensions;
using iLand.Input;
using iLand.Input.Weather;
using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
{
    /** Environmental responses relevant for production of a tree species on resource unit level.
        SpeciesResponse combines data from different sources and converts information about the environment
        into responses of a species. The spatial level is the "ResourceUnit", where homogenetiy of environmental factors
        is assumed. The temporal aggregation depends on the factor, but usually, the daily environmental data is
        aggregated to monthly response values (which subsequently are used during 3-PG production).
        Sources are:
        - vapour pressure deficit (dryness of atmosphere): directly from (daily) weather data
        - soil water status (dryness of soil)
        - temperature: directly from (daily) weather data
        - phenology: @sa Phenology, combines several sources (quasi-monthly)
        - CO2: @sa SpeciesSet::co2Response() based on ambient CO2 level (weather data), nitrogen and soil water responses (monthly)
        - nitrogen: based on the amount of available nitrogen (yearly)
        */
    public class ResourceUnitTreeSpeciesGrowthModifiers
    {
        private readonly ResourceUnit resourceUnit;

        public TreeSpecies Species { get; private init; }

        public float[] CO2ModifierByMonth { get; private init; }
        public float[] SolarRadiationTotalByMonth { get; private init; } // monthly total radiation, MJ/m²
        public float[] SoilWaterModifierByMonth { get; private init; } // monthly average or estimate of daily soilwater response
        public float NitrogenModifierForYear { get; private set; }
        public float[] TemperatureModifierByMonth { get; private init; } // monthly average or estimate of daily temperature response
        public float TotalRadiationForYear { get; private set; } // total radiation of the year, MJ/m²
        public float[] UtilizableRadiationByMonth { get; private init; } // sum of daily radiation*minResponse, MJ/m²
        public float UtilizableRadiationForYear { get; private set; } // yearly sum of utilized radiation, MJ/m²
        public float[] VpdModifierByMonth { get; private init; } // monthly average or estimate of vpd-response

        public ResourceUnitTreeSpeciesGrowthModifiers(ResourceUnit resourceUnit, ResourceUnitTreeSpecies ruSpecies)
        {
            this.resourceUnit = resourceUnit;
            this.Species = ruSpecies.Species;

            this.CO2ModifierByMonth = new float[Constant.Time.MonthsInYear];
            this.SolarRadiationTotalByMonth = new float[Constant.Time.MonthsInYear];
            this.NitrogenModifierForYear = 0.0F;
            this.SoilWaterModifierByMonth = new float[Constant.Time.MonthsInYear];
            this.TemperatureModifierByMonth = new float[Constant.Time.MonthsInYear];
            this.TotalRadiationForYear = 0.0F;
            this.UtilizableRadiationByMonth = new float[Constant.Time.MonthsInYear];
            this.UtilizableRadiationForYear = 0.0F;
            this.VpdModifierByMonth = new float[Constant.Time.MonthsInYear];
        }

        /// Main function that calculates monthly / annual species responses
        public void CalculateMonthlyGrowthModifiers(Landscape landscape)
        {
            this.ZeroMonthlyAndAnnualModifiers(); // reset values

            // nitrogen response: a yearly value based on available nitrogen
            // Calculated before monthly modifiers as calculation of the CO₂ modifier requires the nitrogen modifier.
            if (this.resourceUnit.Soil == null)
            {
                this.NitrogenModifierForYear = 1.0F; // available nitrogen calculations are disabled, so default to making nitrogen non-limiting
            }
            else
            {
                this.NitrogenModifierForYear = this.Species.GetNitrogenModifier(this.resourceUnit.Soil.PlantAvailableNitrogen);
                Debug.Assert((this.NitrogenModifierForYear >= 0.0F) && (this.NitrogenModifierForYear <= 1.0F));
            }

            // calculate monthly modifiers for the current simulation year (January-December calendar year)
            LeafPhenology leafPhenology = this.resourceUnit.Weather.GetPhenology(this.Species.LeafPhenologyID);
            ResourceUnitWaterCycle ruWaterCycle = this.resourceUnit.WaterCycle;
            CO2TimeSeriesMonthly co2timeSeries = landscape.CO2ByMonth;
            WeatherTimeSeries weatherTimeSeries = this.resourceUnit.Weather.TimeSeries;
            if (weatherTimeSeries.Timestep == Timestep.Daily)
            {
                this.CalculateMonthlyGrowthModifiersFromDailyWeather((WeatherTimeSeriesDaily)weatherTimeSeries, leafPhenology, co2timeSeries, ruWaterCycle);
            }
            else if (weatherTimeSeries.Timestep == Timestep.Monthly)
            {
                this.CalculateMonthlyGrowthModifiersFromMonthlyWeather(weatherTimeSeries, leafPhenology, co2timeSeries, ruWaterCycle);
            }
            else
            {
                throw new NotSupportedException("Unhandled weather series timestep " + weatherTimeSeries.Timestep + ".");
            }

            // checks
            #if DEBUG
            for (int monthIndex = 0; monthIndex < Constant.Time.MonthsInYear; ++monthIndex)
            {
                Debug.Assert((this.CO2ModifierByMonth[monthIndex] > 0.0F) && (this.CO2ModifierByMonth[monthIndex] <= 1.3F));
                Debug.Assert((this.SoilWaterModifierByMonth[monthIndex] >= 0.0F) && (this.SoilWaterModifierByMonth[monthIndex] <= 1.000001F));
                Debug.Assert((this.TemperatureModifierByMonth[monthIndex] >= 0.0F) && (this.TemperatureModifierByMonth[monthIndex] <= 1.000001F));
                Debug.Assert((this.VpdModifierByMonth[monthIndex] > 0.0F) && (this.VpdModifierByMonth[monthIndex] <= 1.000001F));
                Debug.Assert((this.UtilizableRadiationByMonth[monthIndex] >= 0.0F) && (this.UtilizableRadiationByMonth[monthIndex] < 5000.0F)); // utilizable radiation will be zero during leaf off months or if the limiting modifier is zero
            }
            #endif

            this.TotalRadiationForYear = this.resourceUnit.Weather.TotalAnnualRadiation; // TODO: is this copy necessary?
        }

        private void CalculateMonthlyGrowthModifiersFromDailyWeather(WeatherTimeSeriesDaily dailyWeatherSeries, LeafPhenology leafPhenology, CO2TimeSeriesMonthly co2timeSeries, ResourceUnitWaterCycle ruWaterCycle)
        {
            int leafOnDayIndex = leafPhenology.LeafOnStartDayOfYearIndex;
            int leafOffDayIndex = leafPhenology.LeafOnEndDayOfYearIndex;
            for (int dayOfYear = 0, weatherDayIndex = dailyWeatherSeries.CurrentYearStartIndex; weatherDayIndex < dailyWeatherSeries.NextYearStartIndex; ++weatherDayIndex, ++dayOfYear)
            {
                int monthOfYearIndex = dailyWeatherSeries.Month[weatherDayIndex] - 1;
                // environmental responses
                float soilWaterResponse = this.Species.GetSoilWaterModifier(ruWaterCycle.SoilWaterPotentialByWeatherTimestepInYear[dayOfYear]);
                this.SoilWaterModifierByMonth[monthOfYearIndex] += soilWaterResponse;

                this.SolarRadiationTotalByMonth[monthOfYearIndex] += dailyWeatherSeries.SolarRadiationTotal[weatherDayIndex];

                float temperatureResponse = this.Species.GetTemperatureModifier(dailyWeatherSeries.TemperatureDaytimeMeanMA1[weatherDayIndex]);
                this.TemperatureModifierByMonth[monthOfYearIndex] += temperatureResponse;

                float vpdResponse = this.Species.GetVpdModifier(dailyWeatherSeries.VpdMeanInKPa[weatherDayIndex]);
                this.VpdModifierByMonth[monthOfYearIndex] += vpdResponse;

                // no utilizable radiation if day is outside of leaf on period so nothing to add
                // If needed, tapering could be used to approxiate leaf out and senescence. Fixed on-off dates are also not reactive to weather.
                if ((dayOfYear >= leafOnDayIndex) && (dayOfYear <= leafOffDayIndex))
                {
                    // environmental responses for the day
                    // combine responses
                    float minimumResponse = MathF.Min(MathF.Min(vpdResponse, temperatureResponse), soilWaterResponse);
                    // calculate utilizable radiation, Eq. 4, http://iland-model.org/primary+production
                    float utilizableRadiation = dailyWeatherSeries.SolarRadiationTotal[weatherDayIndex] * minimumResponse;

                    Debug.Assert((minimumResponse >= 0.0F) && (minimumResponse < 1.000001F), "Minimum of VPD (" + vpdResponse + "), temperature (" + temperatureResponse + "), and soil water (" + soilWaterResponse + ") responses is not in [0, 1].");
                    Debug.Assert((utilizableRadiation >= 0.0F) && (utilizableRadiation < 100.0F)); // sanity upper bound
                    this.UtilizableRadiationByMonth[monthOfYearIndex] += utilizableRadiation;
                }
            }

            // convert sums of daily values to monthly, accumulate annual variables, and find CO₂ modifier
            bool isLeapYear = dailyWeatherSeries.IsCurrentlyLeapYear();
            for (int monthOfYearIndex = 0, co2timestepIndex = co2timeSeries.CurrentYearStartIndex; monthOfYearIndex < Constant.Time.MonthsInYear; ++co2timestepIndex, ++monthOfYearIndex)
            {
                float daysInMonth = (float)DateTimeExtensions.GetDaysInMonth(monthOfYearIndex, isLeapYear);
                float soilWaterModifier = this.SoilWaterModifierByMonth[monthOfYearIndex] / daysInMonth;
                this.SoilWaterModifierByMonth[monthOfYearIndex] = soilWaterModifier;
                this.TemperatureModifierByMonth[monthOfYearIndex] /= daysInMonth;
                this.VpdModifierByMonth[monthOfYearIndex] /= daysInMonth;

                this.UtilizableRadiationForYear += this.UtilizableRadiationByMonth[monthOfYearIndex];

                float atmosphericCO2 = co2timeSeries.CO2ConcentrationInPpm[co2timestepIndex];
                this.CO2ModifierByMonth[monthOfYearIndex] = this.Species.SpeciesSet.GetCarbonDioxideModifier(atmosphericCO2, this.NitrogenModifierForYear, soilWaterModifier);
            }
        }

        private void CalculateMonthlyGrowthModifiersFromMonthlyWeather(WeatherTimeSeries monthlyTimeSeries, LeafPhenology leafPhenology, CO2TimeSeriesMonthly co2timeSeries, ResourceUnitWaterCycle ruWaterCycle)
        {
            for (int weatherMonthIndex = monthlyTimeSeries.CurrentYearStartIndex; weatherMonthIndex < monthlyTimeSeries.NextYearStartIndex; ++weatherMonthIndex)
            {
                int monthOfYearIndex = monthlyTimeSeries.Month[weatherMonthIndex] - 1;
                // environmental responses
                float soilWaterModifier = this.Species.GetSoilWaterModifier(ruWaterCycle.SoilWaterPotentialByWeatherTimestepInYear[monthOfYearIndex]);
                this.SoilWaterModifierByMonth[monthOfYearIndex] += soilWaterModifier;

                this.SolarRadiationTotalByMonth[monthOfYearIndex] += monthlyTimeSeries.SolarRadiationTotal[weatherMonthIndex];

                float temperatureModifier = this.Species.GetTemperatureModifier(monthlyTimeSeries.TemperatureDaytimeMean[weatherMonthIndex]);
                this.TemperatureModifierByMonth[monthOfYearIndex] += temperatureModifier;

                float vpdResponse = this.Species.GetVpdModifier(monthlyTimeSeries.VpdMeanInKPa[weatherMonthIndex]);
                this.VpdModifierByMonth[monthOfYearIndex] += vpdResponse;

                // combine responses
                float minimumResponse = MathF.Min(MathF.Min(vpdResponse, temperatureModifier), soilWaterModifier);

                // estimate utilizable radiation
                float leafOnFraction = leafPhenology.LeafOnFractionByMonth[monthOfYearIndex];
                float utilizableRadiation = monthlyTimeSeries.SolarRadiationTotal[weatherMonthIndex] * leafOnFraction * minimumResponse;

                Debug.Assert((minimumResponse >= 0.0F) && (minimumResponse < 1.000001F), "Minimum of VPD (" + vpdResponse + "), temperature (" + temperatureModifier + "), and soil water (" + soilWaterModifier + ") responses is not in [0, 1].");
                Debug.Assert((utilizableRadiation >= 0.0F) && (utilizableRadiation < 500.0F)); // sanity upper bound
                this.UtilizableRadiationByMonth[monthOfYearIndex] += utilizableRadiation;

                this.UtilizableRadiationForYear += utilizableRadiation;

                // CO₂ response equations require nitrogen and soil water responses in [0 1], so CO₂ response is calculated after finding
                // the average soil water response
                float atmosphericCO2 = co2timeSeries.CO2ConcentrationInPpm[weatherMonthIndex];
                this.CO2ModifierByMonth[monthOfYearIndex] = this.Species.SpeciesSet.GetCarbonDioxideModifier(atmosphericCO2, this.NitrogenModifierForYear, soilWaterModifier);
            }
        }

        /// response calculation called during water cycle
        /// calculates minimum-response of vpd-response and soilwater response
        /// calculate responses for VPD and Soil Water. Return the minimum of those responses
        /// @param psi_kPa psi of the soil in kPa
        /// @param vpd vapor pressure deficit in kPa
        /// @return minimum of soil water and vpd response
        public float GetMostLimitingSoilWaterOrVpdModifier(float psiInKilopascals, float vpdInKiloPascals)
        {
            float waterModifier = this.Species.GetSoilWaterModifier(psiInKilopascals);
            float vpdModifier = this.Species.GetVpdModifier(vpdInKiloPascals);
            return MathF.Min(waterModifier, vpdModifier);
        }

        public void ZeroMonthlyAndAnnualModifiers()
        {
            Array.Fill(this.CO2ModifierByMonth, 0.0F);
            Array.Fill(this.SolarRadiationTotalByMonth, 0.0F);
            Array.Fill(this.SoilWaterModifierByMonth, 0.0F);
            Array.Fill(this.TemperatureModifierByMonth, 0.0F);
            Array.Fill(this.UtilizableRadiationByMonth, 0.0F);
            Array.Fill(this.VpdModifierByMonth, 0.0F);

            this.NitrogenModifierForYear = 0.0F;
            this.TotalRadiationForYear = 0.0F;
            this.UtilizableRadiationForYear = 0.0F;
        }
    }
}
