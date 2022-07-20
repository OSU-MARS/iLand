using System;
using System.Collections.Generic;
using System.Diagnostics;

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
        /// <param name="minTemp">Monthly mean daily minimum air temperature, °C.</param>
        /// <param name="meanTemp">Monthly mean air temperature, °C.</param>
        /// <param name="maxTemp">Monthly mean daily maximum air temperature, °C.</param>
        /// <returns>Monthly mean daily daytime air temperature, °C.</returns>
        /// <remarks>
        /// Estimation here influences estimators in <see cref="iLand.Tree.SaplingEstablishment"/> which consume daily mean air temperatures.
        /// </remarks>
        protected static float EstimateDaytimeMeanAirTemperature(float minTemp, float meanTemp, float maxTemp)
        {
            // Based on multiple linear regression of the primary and secondary meteorology stations on the HJ Andrews Research Forest, p < 0.001,
            // adjR² = 0.993, MAE = 0.46 °C. Daytime mean air temperature is nearly the same as mean daily temperature in January and commonly 2-3
            // °C warmer in the summer.
            //
            // Regression is nearly unbiased up to 19 °C (<0.1 °C), overestimates by 0.5-0.7 °C for months averaging 20-24 °C (mean response from
            // GAM smoothing). Upper end error can be reduced with high order or exponential terms but these lack a plausible physical basis and
            // are unlikely to be stable over hotter or cooler sites beyond the -3 to 24 °C fitting range. Total solar radiation, relative humidity,
            // and precipitation may be useful additional predictors here.
            //
            // HJ Andrews dataset MS00101 version 9 (October 2019), https://andlter.forestry.oregonstate.edu/data/catalog/datacatalog.aspx.
            return -0.89905F - 0.22593F * minTemp + 1.20802F * meanTemp + 0.07674F * maxTemp;
        }

        /// <summary>
        /// Estimates the monthly mean vapor pressure deficit from other monthly variables.
        /// </summary>
        /// <param name="minTemp">Monthly mean daily minimum air temperature, °C.</param>
        /// <param name="meanTemp">Monthly mean air temperature, °C.</param>
        /// <param name="maxTemp">Monthly mean daily maximum air temperature, °C.</param>
        /// <param name="relativeHumidityMean">Monthly mean daily relative humidity, %.</param>
        /// <returns>Monthly mean vapor pressure deficit, kPa.</returns>
        protected static float EstimateVaporPressureDeficit(float minTemp, float meanTemp, float maxTemp, float relativeHumidityMean)
        {
            // Based on multiple linear regression of the primary and secondary meteorology stations on the HJ Andrews Research Forest, p < 0.001,
            // adjR² = 0.990, MAE = 21 Pa. Error appears likely to increase more or less linearly with VPD due to differences among stations'
            // response slopes.
            float expTmin = MathF.Exp(17.27F * minTemp / (minTemp + 237.3F));
            float expTmean = MathF.Exp(17.27F * meanTemp / (meanTemp + 237.3F));
            float expTmax = MathF.Exp(17.27F * maxTemp / (maxTemp + 237.3F));
            float vpdFractionMean = 1.0F - 0.01F * relativeHumidityMean;
            float expTvpdFractionMean = expTmean * vpdFractionMean;
            float vpdMean = 0.024232F - 0.219306F * expTmin - 0.186437F * vpdFractionMean + 0.201600F * expTmean - 0.028642F * expTmax +
                            0.692668F * expTvpdFractionMean - 0.148042F * expTvpdFractionMean * expTvpdFractionMean +
                            0.175425F * expTmax * vpdFractionMean;
            if (vpdMean < 0.0F)
            {
                // regression is unconstrained so occasional minor excursions into negative VPD are expected and suppressed
                Debug.Assert(vpdMean >= -0.025F);
                vpdMean = 0.0F;
            }
            return vpdMean;
        }
    }
}
