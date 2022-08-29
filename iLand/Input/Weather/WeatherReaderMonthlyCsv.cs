using System;
using System.Diagnostics;
using System.Globalization;

namespace iLand.Input.Weather
{
    internal class WeatherReaderMonthlyCsv : WeatherReaderMonthly
    {
        /// <summary>
        /// Read monthly weather data from ClimateNA in CSV (comma separated value) format.
        /// </summary>
        /// <remarks>
        /// See unit conversion notes in <see cref="WeatherReaderMonthly">.
        /// </remarks>
        public WeatherReaderMonthlyCsv(string weatherFilePath, Int16 startYear)
        {
            // TODO: support for temperature shifts and precipitation multipliers for sensitivity analysis
            CsvFile weatherFile = new(weatherFilePath);
            WeatherHeaderMonthlyCsv weatherHeader = new(weatherFile);

            string? currentWeatherID = null;
            WeatherTimeSeriesMonthly? monthlyWeather = null;
            int longestTimeSeriesLengthInMonths = -1;

            weatherFile.Parse((row) =>
            {
                Int16 year = Int16.Parse(row[weatherHeader.Year], NumberStyles.Integer);
                if (year < startYear)
                {
                    return;
                }

                ReadOnlySpan<char> weatherID = row[weatherHeader.ID];
                if (MemoryExtensions.Equals(weatherID, currentWeatherID, StringComparison.OrdinalIgnoreCase) == false)
                {
                    currentWeatherID = weatherID.ToString();
                    if (this.MonthlyWeatherByID.TryGetValue(currentWeatherID, out monthlyWeather) == false)
                    {
                        monthlyWeather = new(Timestep.Monthly);
                        this.MonthlyWeatherByID.Add(currentWeatherID, monthlyWeather);
                    }
                }
                Debug.Assert(monthlyWeather != null);

                if (monthlyWeather.Capacity - 12 < monthlyWeather.Count)
                {
                    int estimatedCapacity = WeatherReaderMonthly.EstimateTimeSeriesLength(monthlyWeather, longestTimeSeriesLengthInMonths);
                    monthlyWeather.Resize(estimatedCapacity);
                }

                // gather and copy year, month, precipitation, snow, solar radiation, and maximum and minimum temperatures
                Span<byte> monthOfYear = stackalloc byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
                Span<float> precipitationTotalByMonthInMM = stackalloc float[12];
                Span<float> relativeHumidityMeanByMonth = stackalloc float[12];
                Span<float> snowTotalByMonthInMM = stackalloc float[12];
                Span<float> solarRadiationTotalByMonth = stackalloc float[12];
                Span<float> temperatureDaytimeMeanByMonth = stackalloc float[12];
                Span<float> temperatureMaxByMonth = stackalloc float[12];
                Span<float> temperatureMeanByMonth = stackalloc float[12];
                Span<float> temperatureMinByMonth = stackalloc float[12];
                Span<float> vaporPressureDeficitMeanInKPa = stackalloc float[12];

                int januaryDestinationIndex = monthlyWeather.Count;
                monthlyWeather.Year.AsSpan().Slice(januaryDestinationIndex, Constant.Time.MonthsInYear).Fill(year);
                monthOfYear.CopyTo(monthlyWeather.Month.AsSpan()[januaryDestinationIndex..]);

                precipitationTotalByMonthInMM[0] = Single.Parse(row[weatherHeader.Precipitation01], NumberStyles.Float);
                precipitationTotalByMonthInMM[1] = Single.Parse(row[weatherHeader.Precipitation02], NumberStyles.Float);
                precipitationTotalByMonthInMM[2] = Single.Parse(row[weatherHeader.Precipitation03], NumberStyles.Float);
                precipitationTotalByMonthInMM[3] = Single.Parse(row[weatherHeader.Precipitation04], NumberStyles.Float);
                precipitationTotalByMonthInMM[4] = Single.Parse(row[weatherHeader.Precipitation05], NumberStyles.Float);
                precipitationTotalByMonthInMM[5] = Single.Parse(row[weatherHeader.Precipitation06], NumberStyles.Float);
                precipitationTotalByMonthInMM[6] = Single.Parse(row[weatherHeader.Precipitation07], NumberStyles.Float);
                precipitationTotalByMonthInMM[7] = Single.Parse(row[weatherHeader.Precipitation08], NumberStyles.Float);
                precipitationTotalByMonthInMM[8] = Single.Parse(row[weatherHeader.Precipitation09], NumberStyles.Float);
                precipitationTotalByMonthInMM[9] = Single.Parse(row[weatherHeader.Precipitation10], NumberStyles.Float);
                precipitationTotalByMonthInMM[10] = Single.Parse(row[weatherHeader.Precipitation11], NumberStyles.Float);
                precipitationTotalByMonthInMM[11] = Single.Parse(row[weatherHeader.Precipitation12], NumberStyles.Float);
                precipitationTotalByMonthInMM.CopyTo(monthlyWeather.PrecipitationTotalInMM.AsSpan()[januaryDestinationIndex..]);

                snowTotalByMonthInMM[0] = Single.Parse(row[weatherHeader.Snow01], NumberStyles.Float);
                snowTotalByMonthInMM[1] = Single.Parse(row[weatherHeader.Snow02], NumberStyles.Float);
                snowTotalByMonthInMM[2] = Single.Parse(row[weatherHeader.Snow03], NumberStyles.Float);
                snowTotalByMonthInMM[3] = Single.Parse(row[weatherHeader.Snow04], NumberStyles.Float);
                snowTotalByMonthInMM[4] = Single.Parse(row[weatherHeader.Snow05], NumberStyles.Float);
                snowTotalByMonthInMM[5] = Single.Parse(row[weatherHeader.Snow06], NumberStyles.Float);
                snowTotalByMonthInMM[6] = Single.Parse(row[weatherHeader.Snow07], NumberStyles.Float);
                snowTotalByMonthInMM[7] = Single.Parse(row[weatherHeader.Snow08], NumberStyles.Float);
                snowTotalByMonthInMM[8] = Single.Parse(row[weatherHeader.Snow09], NumberStyles.Float);
                snowTotalByMonthInMM[9] = Single.Parse(row[weatherHeader.Snow10], NumberStyles.Float);
                snowTotalByMonthInMM[10] = Single.Parse(row[weatherHeader.Snow11], NumberStyles.Float);
                snowTotalByMonthInMM[11] = Single.Parse(row[weatherHeader.Snow12], NumberStyles.Float);
                snowTotalByMonthInMM.CopyTo(monthlyWeather.SnowTotalInMM.AsSpan()[januaryDestinationIndex..]);

                solarRadiationTotalByMonth[0] = Single.Parse(row[weatherHeader.SolarRadiation01], NumberStyles.Float);
                solarRadiationTotalByMonth[1] = Single.Parse(row[weatherHeader.SolarRadiation02], NumberStyles.Float);
                solarRadiationTotalByMonth[2] = Single.Parse(row[weatherHeader.SolarRadiation03], NumberStyles.Float);
                solarRadiationTotalByMonth[3] = Single.Parse(row[weatherHeader.SolarRadiation04], NumberStyles.Float);
                solarRadiationTotalByMonth[4] = Single.Parse(row[weatherHeader.SolarRadiation05], NumberStyles.Float);
                solarRadiationTotalByMonth[5] = Single.Parse(row[weatherHeader.SolarRadiation06], NumberStyles.Float);
                solarRadiationTotalByMonth[6] = Single.Parse(row[weatherHeader.SolarRadiation07], NumberStyles.Float);
                solarRadiationTotalByMonth[7] = Single.Parse(row[weatherHeader.SolarRadiation08], NumberStyles.Float);
                solarRadiationTotalByMonth[8] = Single.Parse(row[weatherHeader.SolarRadiation09], NumberStyles.Float);
                solarRadiationTotalByMonth[9] = Single.Parse(row[weatherHeader.SolarRadiation10], NumberStyles.Float);
                solarRadiationTotalByMonth[10] = Single.Parse(row[weatherHeader.SolarRadiation11], NumberStyles.Float);
                solarRadiationTotalByMonth[11] = Single.Parse(row[weatherHeader.SolarRadiation12], NumberStyles.Float);
                solarRadiationTotalByMonth.CopyTo(monthlyWeather.SolarRadiationTotal.AsSpan()[januaryDestinationIndex..]);

                temperatureMaxByMonth[0] = Single.Parse(row[weatherHeader.TemperatureMax01], NumberStyles.Float);
                temperatureMaxByMonth[1] = Single.Parse(row[weatherHeader.TemperatureMax02], NumberStyles.Float);
                temperatureMaxByMonth[2] = Single.Parse(row[weatherHeader.TemperatureMax03], NumberStyles.Float);
                temperatureMaxByMonth[3] = Single.Parse(row[weatherHeader.TemperatureMax04], NumberStyles.Float);
                temperatureMaxByMonth[4] = Single.Parse(row[weatherHeader.TemperatureMax05], NumberStyles.Float);
                temperatureMaxByMonth[5] = Single.Parse(row[weatherHeader.TemperatureMax06], NumberStyles.Float);
                temperatureMaxByMonth[6] = Single.Parse(row[weatherHeader.TemperatureMax07], NumberStyles.Float);
                temperatureMaxByMonth[7] = Single.Parse(row[weatherHeader.TemperatureMax08], NumberStyles.Float);
                temperatureMaxByMonth[8] = Single.Parse(row[weatherHeader.TemperatureMax09], NumberStyles.Float);
                temperatureMaxByMonth[9] = Single.Parse(row[weatherHeader.TemperatureMax10], NumberStyles.Float);
                temperatureMaxByMonth[10] = Single.Parse(row[weatherHeader.TemperatureMax11], NumberStyles.Float);
                temperatureMaxByMonth[11] = Single.Parse(row[weatherHeader.TemperatureMax12], NumberStyles.Float);
                temperatureMaxByMonth.CopyTo(monthlyWeather.TemperatureMax.AsSpan()[januaryDestinationIndex..]);

                temperatureMinByMonth[0] = Single.Parse(row[weatherHeader.TemperatureMin01], NumberStyles.Float);
                temperatureMinByMonth[1] = Single.Parse(row[weatherHeader.TemperatureMin02], NumberStyles.Float);
                temperatureMinByMonth[2] = Single.Parse(row[weatherHeader.TemperatureMin03], NumberStyles.Float);
                temperatureMinByMonth[3] = Single.Parse(row[weatherHeader.TemperatureMin04], NumberStyles.Float);
                temperatureMinByMonth[4] = Single.Parse(row[weatherHeader.TemperatureMin05], NumberStyles.Float);
                temperatureMinByMonth[5] = Single.Parse(row[weatherHeader.TemperatureMin06], NumberStyles.Float);
                temperatureMinByMonth[6] = Single.Parse(row[weatherHeader.TemperatureMin07], NumberStyles.Float);
                temperatureMinByMonth[7] = Single.Parse(row[weatherHeader.TemperatureMin08], NumberStyles.Float);
                temperatureMinByMonth[8] = Single.Parse(row[weatherHeader.TemperatureMin09], NumberStyles.Float);
                temperatureMinByMonth[9] = Single.Parse(row[weatherHeader.TemperatureMin10], NumberStyles.Float);
                temperatureMinByMonth[10] = Single.Parse(row[weatherHeader.TemperatureMin11], NumberStyles.Float);
                temperatureMinByMonth[11] = Single.Parse(row[weatherHeader.TemperatureMin12], NumberStyles.Float);
                temperatureMinByMonth.CopyTo(monthlyWeather.TemperatureMin.AsSpan()[januaryDestinationIndex..]);

                // estimate daytime mean temperature and vapor pressure deficit
                temperatureMeanByMonth[0] = Single.Parse(row[weatherHeader.TemperatureMean01], NumberStyles.Float);
                temperatureMeanByMonth[1] = Single.Parse(row[weatherHeader.TemperatureMean02], NumberStyles.Float);
                temperatureMeanByMonth[2] = Single.Parse(row[weatherHeader.TemperatureMean03], NumberStyles.Float);
                temperatureMeanByMonth[3] = Single.Parse(row[weatherHeader.TemperatureMean04], NumberStyles.Float);
                temperatureMeanByMonth[4] = Single.Parse(row[weatherHeader.TemperatureMean05], NumberStyles.Float);
                temperatureMeanByMonth[5] = Single.Parse(row[weatherHeader.TemperatureMean06], NumberStyles.Float);
                temperatureMeanByMonth[6] = Single.Parse(row[weatherHeader.TemperatureMean07], NumberStyles.Float);
                temperatureMeanByMonth[7] = Single.Parse(row[weatherHeader.TemperatureMean08], NumberStyles.Float);
                temperatureMeanByMonth[8] = Single.Parse(row[weatherHeader.TemperatureMean09], NumberStyles.Float);
                temperatureMeanByMonth[9] = Single.Parse(row[weatherHeader.TemperatureMean10], NumberStyles.Float);
                temperatureMeanByMonth[10] = Single.Parse(row[weatherHeader.TemperatureMean11], NumberStyles.Float);
                temperatureMeanByMonth[11] = Single.Parse(row[weatherHeader.TemperatureMean12], NumberStyles.Float);

                WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(temperatureMinByMonth, temperatureMeanByMonth, temperatureMaxByMonth, temperatureDaytimeMeanByMonth);
                temperatureDaytimeMeanByMonth.CopyTo(monthlyWeather.TemperatureDaytimeMean.AsSpan()[januaryDestinationIndex..]);

                relativeHumidityMeanByMonth[0] = Single.Parse(row[weatherHeader.RelativeHumidityMean01], NumberStyles.Float);
                relativeHumidityMeanByMonth[1] = Single.Parse(row[weatherHeader.RelativeHumidityMean02], NumberStyles.Float);
                relativeHumidityMeanByMonth[2] = Single.Parse(row[weatherHeader.RelativeHumidityMean03], NumberStyles.Float);
                relativeHumidityMeanByMonth[3] = Single.Parse(row[weatherHeader.RelativeHumidityMean04], NumberStyles.Float);
                relativeHumidityMeanByMonth[4] = Single.Parse(row[weatherHeader.RelativeHumidityMean05], NumberStyles.Float);
                relativeHumidityMeanByMonth[5] = Single.Parse(row[weatherHeader.RelativeHumidityMean06], NumberStyles.Float);
                relativeHumidityMeanByMonth[6] = Single.Parse(row[weatherHeader.RelativeHumidityMean07], NumberStyles.Float);
                relativeHumidityMeanByMonth[7] = Single.Parse(row[weatherHeader.RelativeHumidityMean08], NumberStyles.Float);
                relativeHumidityMeanByMonth[8] = Single.Parse(row[weatherHeader.RelativeHumidityMean09], NumberStyles.Float);
                relativeHumidityMeanByMonth[9] = Single.Parse(row[weatherHeader.RelativeHumidityMean10], NumberStyles.Float);
                relativeHumidityMeanByMonth[10] = Single.Parse(row[weatherHeader.RelativeHumidityMean11], NumberStyles.Float);
                relativeHumidityMeanByMonth[11] = Single.Parse(row[weatherHeader.RelativeHumidityMean12], NumberStyles.Float);

                WeatherReaderMonthly.EstimateVaporPressureDeficit(temperatureMinByMonth, temperatureMeanByMonth, temperatureMaxByMonth, relativeHumidityMeanByMonth, vaporPressureDeficitMeanInKPa);
                vaporPressureDeficitMeanInKPa.CopyTo(monthlyWeather.VpdMeanInKPa.AsSpan()[januaryDestinationIndex..]);

                // complete year
                monthlyWeather.Validate(monthlyWeather.Count, Constant.Time.MonthsInYear);

                monthlyWeather.Count += 12;
                if (monthlyWeather.Count > longestTimeSeriesLengthInMonths)
                {
                    longestTimeSeriesLengthInMonths = monthlyWeather.Count;
                }
            });

            foreach (WeatherTimeSeriesMonthly monthlyWeatherSeriesToValidate in this.MonthlyWeatherByID.Values)
            {
                monthlyWeatherSeriesToValidate.Validate(0, monthlyWeatherSeriesToValidate.Count);
            }
        }
    }
}
