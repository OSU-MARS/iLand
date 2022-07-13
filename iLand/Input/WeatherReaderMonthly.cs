using System.Collections.Generic;

namespace iLand.Input
{
    internal class WeatherReaderMonthly
    {
        public Dictionary<string, WeatherTimeSeriesMonthly> MonthlyWeatherByID { get; private init; }

        public WeatherReaderMonthly()
        {
            this.MonthlyWeatherByID = new();
        }

        /// <summary>
        /// Estimates the monthly mean daily daytime air temperature from other monthly air temperatures.
        /// </summary>
        /// <param name="minTemp">Monthly mean daily minimum air temperature.</param>
        /// <param name="meanTemp">Monthly mean air temperature.</param>
        /// <param name="maxTemp">Monthly mean daily maximum air temperature.</param>
        /// <returns>Monthly mean daily daytime air temperature.</returns>
        protected static float EstimateDaytimeMeanAirTemperature(float minTemp, float meanTemp, float maxTemp)
        {
            // Based on multiple linear regression of the primary meteorology stations on the HJ Andrews Research Forest, p < 0.001, adjR² = 0.993, 
            // MAE = 0.46 °C. Daytime mean air temperature is nearly the same as mean daily temperature in January and commonly 2-3 °C warmer
            // in the summer.
            //
            // Regression is nearly unbiased up to 19 °C (<0.1 °C), overestimates by 0.5-0.7 °C for months averaging 20-24 °C (mean response from
            // GAM smoothing). Upper end error can be reduced with high order or exponential terms but these lack a plausible physical basis and
            // are unlikely to be stable over hotter or cooler sites beyond the -3 to 24 °C fitting range. Total solar radiation, relative humidity,
            // and precipitation may be useful additional predictors here.
            //
            // HJ Andrews dataset MS00101 version 9 (October 2019), https://andlter.forestry.oregonstate.edu/data/catalog/datacatalog.aspx.
            return -0.89905F - 0.22593F * minTemp + 1.20802F * meanTemp + 0.07674F * maxTemp;
        }
    }
}
