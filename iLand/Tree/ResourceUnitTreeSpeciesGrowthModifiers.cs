using iLand.Input;
using iLand.Tool;
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
        public ResourceUnit ResourceUnit { get; private init; }
        public TreeSpecies Species { get; private init; }

        public float[] CO2ModifierByMonth { get; private init; }
        public float[] GlobalRadiationByMonth { get; private init; } // radiation sum in MJ/m²
        public float[] SoilWaterModifierByMonth { get; private init; } // monthly average or estimate of daily soilwater response
        public float NitrogenModifierForYear { get; private set; }
        public float[] TemperatureModifierByMonth { get; private init; } // monthly average or estimate of daily temperature response
        public float TotalRadiationForYear { get; private set; } // total radiation of the year (MJ/m²)
        public float[] UtilizableRadiationByMonth { get; private init; } // sum of daily radiation*minResponse (MJ/m²)
        public float UtilizableRadiationForYear { get; private set; } // yearly sum of utilized radiation (MJ/²)
        public float[] VpdModifierByMonth { get; private init; } // monthly average or estimate of vpd-response

        public ResourceUnitTreeSpeciesGrowthModifiers(ResourceUnit ru, ResourceUnitTreeSpecies ruSpecies)
        {
            this.Species = ruSpecies.Species;
            this.ResourceUnit = ru;

            this.CO2ModifierByMonth = new float[Constant.MonthsInYear];
            this.GlobalRadiationByMonth = new float[Constant.MonthsInYear];
            this.NitrogenModifierForYear = 0.0F;
            this.SoilWaterModifierByMonth = new float[Constant.MonthsInYear];
            this.TemperatureModifierByMonth = new float[Constant.MonthsInYear];
            this.TotalRadiationForYear = 0.0F;
            this.UtilizableRadiationByMonth = new float[Constant.MonthsInYear];
            this.UtilizableRadiationForYear = 0.0F;
            this.VpdModifierByMonth = new float[Constant.MonthsInYear];
        }

        /// Main function that calculates monthly / annual species responses
        public void CalculateMonthlyGrowthModifiers(Weather weather)
        {
            this.ZeroMonthlyAndAnnualModifiers(); // reset values

            // nitrogen response: a yearly value based on available nitrogen
            if (this.ResourceUnit.Soil == null)
            {
                this.NitrogenModifierForYear = 1.0F; // available nitrogen calculations are disabled, so default to making nitrogen non-limiting
            }
            else
            {
                this.NitrogenModifierForYear = this.Species.GetNitrogenModifier(this.ResourceUnit.Soil.PlantAvailableNitrogen);
                Debug.Assert(this.NitrogenModifierForYear >= 0.0);
            }

            // calculate monthly responses
            LeafPhenology leafPhenology = this.ResourceUnit.Weather.GetPhenology(this.Species.LeafPhenologyID);
            WaterCycle ruWaterCycle = this.ResourceUnit.WaterCycle;
            WeatherTimeSeries weatherTimeSeries = this.ResourceUnit.Weather.TimeSeries;
            if (weatherTimeSeries.Timestep == Timestep.Daily)
            {
                this.CalculateMonthlyGrowthModifiersFromDailyWeather((WeatherTimeSeriesDaily)weatherTimeSeries, leafPhenology, ruWaterCycle);
            }
            else if (weatherTimeSeries.Timestep == Timestep.Monthly)
            {
                this.CalculateMonthlyGrowthModifiersFromMonthlyWeather(weatherTimeSeries, leafPhenology, ruWaterCycle);
            }
            else
            {
                throw new NotSupportedException("Unhandled weather series timestep " + weatherTimeSeries.Timestep + ".");
            }

            // CO₂ and checks
            for (int monthIndex = 0; monthIndex < Constant.MonthsInYear; ++monthIndex)
            {
                // CO₂ response equations assume nitrogen and soil water responses are in [0 1], so CO₂ response is calculated after finding
                // the average soil water response
                // TODO: fold this into CalculateMonthlyGrowthModifiersFrom*Weather() once monthly CO₂ is supported
                float ambientCO2 = weather.AtmosphericCO2ConcentrationInPpm; // CO₂ level of first day of year (CO₂ is static)
                this.CO2ModifierByMonth[monthIndex] = this.Species.SpeciesSet.GetCarbonDioxideModifier(ambientCO2, this.NitrogenModifierForYear, this.SoilWaterModifierByMonth[monthIndex]);

                Debug.Assert((this.CO2ModifierByMonth[monthIndex] > 0.0F) && (this.CO2ModifierByMonth[monthIndex] <= 1.3F));
                Debug.Assert((this.SoilWaterModifierByMonth[monthIndex] >= 0.0F) && (this.SoilWaterModifierByMonth[monthIndex] <= 1.000001F));
                Debug.Assert((this.TemperatureModifierByMonth[monthIndex] >= 0.0F) && (this.TemperatureModifierByMonth[monthIndex] <= 1.000001F));
                Debug.Assert((this.VpdModifierByMonth[monthIndex] > 0.0F) && (this.VpdModifierByMonth[monthIndex] <= 1.000001F));
                Debug.Assert(this.UtilizableRadiationByMonth[monthIndex] >= 0.0F); // utilizable radiation will be zero if the limiting response is zero
            }

            this.TotalRadiationForYear = this.ResourceUnit.Weather.TotalAnnualRadiation; // TODO: is this copy necessary?
        }

        private void CalculateMonthlyGrowthModifiersFromDailyWeather(WeatherTimeSeriesDaily dailyWeatherSeries, LeafPhenology leafPhenology, WaterCycle ruWaterCycle)
        {
            int leafOnDayIndex = leafPhenology.LeafOnStartDayOfYearIndex;
            int leafOffDayIndex = leafPhenology.LeafOnEndDayOfYearIndex;
            for (int dayOfYear = 0, weatherDayIndex = dailyWeatherSeries.CurrentYearStartIndex; weatherDayIndex < dailyWeatherSeries.NextYearStartIndex; ++weatherDayIndex, ++dayOfYear)
            {
                int monthIndex = dailyWeatherSeries.Month[weatherDayIndex] - 1;
                // environmental responses
                this.GlobalRadiationByMonth[monthIndex] += dailyWeatherSeries.SolarRadiationTotal[weatherDayIndex];

                float soilWaterResponse = this.Species.GetSoilWaterModifier(ruWaterCycle.SoilWaterPotentialByWeatherTimestepInYear[dayOfYear]);
                this.SoilWaterModifierByMonth[monthIndex] += soilWaterResponse;

                float temperatureResponse = this.Species.GetTemperatureModifier(dailyWeatherSeries.TemperatureDaytimeMeanMA1[weatherDayIndex]);
                this.TemperatureModifierByMonth[monthIndex] += temperatureResponse;

                float vpdResponse = this.Species.GetVpdModifier(dailyWeatherSeries.VpdMeanInKPa[weatherDayIndex]);
                this.VpdModifierByMonth[monthIndex] += vpdResponse;

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
                    this.UtilizableRadiationByMonth[monthIndex] += utilizableRadiation;
                }
            }

            // convert sums of daily values to monthly and accumulate annual variables
            bool isLeapYear = dailyWeatherSeries.IsCurrentlyLeapYear();
            for (int monthIndex = 0; monthIndex < Constant.MonthsInYear; ++monthIndex)
            {
                float daysInMonth = (float)DateTimeExtensions.GetDaysInMonth(monthIndex, isLeapYear);
                this.SoilWaterModifierByMonth[monthIndex] /= daysInMonth;
                this.TemperatureModifierByMonth[monthIndex] /= daysInMonth;
                this.VpdModifierByMonth[monthIndex] /= daysInMonth;

                this.UtilizableRadiationForYear += this.UtilizableRadiationByMonth[monthIndex];
            }
        }

        private void CalculateMonthlyGrowthModifiersFromMonthlyWeather(WeatherTimeSeries monthlyTimeSeries, LeafPhenology leafPhenology, WaterCycle ruWaterCycle)
        {
            for (int weatherMonthIndex = monthlyTimeSeries.CurrentYearStartIndex; weatherMonthIndex < monthlyTimeSeries.NextYearStartIndex; ++weatherMonthIndex)
            {
                int monthOfYearIndex = monthlyTimeSeries.Month[weatherMonthIndex] - 1;
                // environmental responses
                this.GlobalRadiationByMonth[monthOfYearIndex] += monthlyTimeSeries.SolarRadiationTotal[weatherMonthIndex];

                float soilWaterResponse = this.Species.GetSoilWaterModifier(ruWaterCycle.SoilWaterPotentialByWeatherTimestepInYear[monthOfYearIndex]);
                this.SoilWaterModifierByMonth[monthOfYearIndex] += soilWaterResponse;

                float temperatureResponse = this.Species.GetTemperatureModifier(monthlyTimeSeries.TemperatureDaytimeMean[weatherMonthIndex]);
                this.TemperatureModifierByMonth[monthOfYearIndex] += temperatureResponse;

                float vpdResponse = this.Species.GetVpdModifier(monthlyTimeSeries.VpdMeanInKPa[weatherMonthIndex]);
                this.VpdModifierByMonth[monthOfYearIndex] += vpdResponse;

                // combine responses
                float minimumResponse = MathF.Min(MathF.Min(vpdResponse, temperatureResponse), soilWaterResponse);

                // estimate utilizable radiation
                float leafOnFraction = leafPhenology.LeafOnFractionByMonth[monthOfYearIndex];
                float utilizableRadiation = monthlyTimeSeries.SolarRadiationTotal[weatherMonthIndex] * leafOnFraction * minimumResponse;

                Debug.Assert((minimumResponse >= 0.0F) && (minimumResponse < 1.000001F), "Minimum of VPD (" + vpdResponse + "), temperature (" + temperatureResponse + "), and soil water (" + soilWaterResponse + ") responses is not in [0, 1].");
                Debug.Assert((utilizableRadiation >= 0.0F) && (utilizableRadiation < 100.0F)); // sanity upper bound
                this.UtilizableRadiationByMonth[monthOfYearIndex] += utilizableRadiation;

                this.UtilizableRadiationForYear += utilizableRadiation;
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
            Array.Fill(this.GlobalRadiationByMonth, 0.0F);
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
