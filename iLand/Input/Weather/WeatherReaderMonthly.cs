using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Input.Weather
{
    /// <summary>
    /// Convert monthly weather time series from ClimateNA (https://climatena.ca/) or a similar downscaling tool (ClimateAP, etc.) to 
    /// iLand input variables: daily mean minimum, average, and maximum temperatures, total monthly precipitation (rain and snow), 
    /// total monthly solar radiation, and vapor pressure deficit (estimated from temperatures and relative humidity at load time).
    /// </summary>
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
        /// <param name="minTempByMonth">Monthly mean daily minimum air temperature, °C.</param>
        /// <param name="meanTempByMonth">Monthly mean air temperature, °C.</param>
        /// <param name="maxTempByMonth">Monthly mean daily maximum air temperature, °C.</param>
        /// <param name="daytimeMeanAirTemperatureByMonth">Monthly mean daily daytime air temperature, °C.</returns>
        /// <remarks>
        /// Estimation here influences estimators in <see cref="iLand.Tree.SaplingEstablishment"/> which consume daily mean air temperatures.
        /// </remarks>
        protected static void EstimateDaytimeMeanAirTemperature(ReadOnlySpan<float> minTempByMonth, ReadOnlySpan<float> meanTempByMonth, ReadOnlySpan<float> maxTempByMonth, Span<float> daytimeMeanAirTemperatureByMonth)
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
            for (int monthIndex = 0; monthIndex < Constant.MonthsInYear; ++monthIndex)
            {
                daytimeMeanAirTemperatureByMonth[monthIndex] =  -0.89905F - 0.22593F * minTempByMonth[monthIndex] + 1.20802F * meanTempByMonth[monthIndex] + 0.07674F * maxTempByMonth[monthIndex];
            }
        }

        protected static int EstimateTimeSeriesLength(WeatherTimeSeriesMonthly monthlyWeather, int longestTimeSeriesLengthInMonths)
        {
            // attempt to learn length of weather series as the first series is read
            // Learing increases read efficiency by reducing memory reallocation to extend arrays. It's most effective when
            // the input is ordered by time series and all series are of equal length. In this case, the exact length is
            // learnt from the first series loaded and all other series' arrays need only be allocated once.
            if ((longestTimeSeriesLengthInMonths < 0) || (monthlyWeather.Capacity >= longestTimeSeriesLengthInMonths))
            {
                return monthlyWeather.Capacity + Constant.Data.DefaultMonthlyAllocationIncrement;
            }
            return longestTimeSeriesLengthInMonths;
        }

        /// <summary>
        /// Estimates the monthly mean vapor pressure deficit from other monthly variables.
        /// </summary>
        /// <param name="minTempByMonth">Monthly mean daily minimum air temperature, °C.</param>
        /// <param name="meanTempByMonth">Monthly mean air temperature, °C.</param>
        /// <param name="maxTempByMonth">Monthly mean daily maximum air temperature, °C.</param>
        /// <param name="relativeHumidityMeanByMonth">Monthly mean daily relative humidity, %.</param>
        /// <param name="vpdMeanByMonth">Monthly mean vapor pressure deficit, kPa.</returns>
        protected static void EstimateVaporPressureDeficit(ReadOnlySpan<float> minTempByMonth, ReadOnlySpan<float> meanTempByMonth, ReadOnlySpan<float> maxTempByMonth, ReadOnlySpan<float> relativeHumidityMeanByMonth, Span<float> vpdMeanByMonth)
        {
            // Based on multiple linear regression of the primary and secondary meteorology stations on the HJ Andrews Research Forest, p < 0.001,
            // adjR² = 0.990, MAE = 21 Pa. Error appears likely to increase more or less linearly with VPD due to differences among stations'
            // response slopes.
            for (int monthIndex = 0; monthIndex < Constant.MonthsInYear; ++monthIndex)
            {
                float minTemp = minTempByMonth[monthIndex];
                float expTmin = MathF.Exp(17.27F * minTemp / (minTemp + 237.3F));
                float meanTemp = meanTempByMonth[monthIndex];
                float expTmean = MathF.Exp(17.27F * meanTemp / (meanTemp + 237.3F));
                float maxTemp = maxTempByMonth[monthIndex];
                float expTmax = MathF.Exp(17.27F * maxTemp / (maxTemp + 237.3F));
                float vpdFractionMean = 1.0F - 0.01F * relativeHumidityMeanByMonth[monthIndex];
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

                vpdMeanByMonth[monthIndex] = vpdMean;
            }
        }
    }
}
