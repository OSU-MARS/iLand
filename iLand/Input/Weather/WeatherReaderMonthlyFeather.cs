using Apache.Arrow;
using Apache.Arrow.Ipc;
using System;
using System.IO;

namespace iLand.Input.Weather
{
    internal class WeatherReaderMonthlyFeather : WeatherReaderMonthly
    {
        /// <summary>
        /// Read monthly weather data from ClimateNA transcoded from ClimateNA .csvs to .feather in R (readr::read_csv() and arrow::write_feather()).
        /// </summary>
        /// <remarks>
        /// See unit conversion notes in <see cref="WeatherReaderMonthly">.
        /// </remarks>
        public WeatherReaderMonthlyFeather(string weatherFilePath, int startYear)
        {
            // Arrow 9.0.0 supports only uncompressed feather: https://issues.apache.org/jira/browse/ARROW-17062
            using FileStream weatherStream = new(weatherFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize);
            using ArrowFileReader weatherFile = new(weatherStream); // ArrowFileReader.IsFileValid is false until a batch is read

            WeatherTimeSeriesMonthly? monthlyWeather = null;
            int longestTimeSeriesLengthInMonths = -1;

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
            for (RecordBatch? batch = weatherFile.ReadNextRecordBatch(); batch != null; batch = weatherFile.ReadNextRecordBatch())
            {
                // unpack fields to amortize span reinterpretation overhead
                // Added cost of reinterpretation on every value retrieved is ~2 s/GB of weather data (AMD Zen 3 single core @ 4.8 GHz,
                // PCIe 3.0 x4 SSD).
                WeatherArrowBatchMonthly fields = new(batch);
                ReadOnlySpan<Int16> yearField = fields.Year.Values;
                ReadOnlySpan<float> precipFieldJanuary = fields.Precipitation01.Values;
                ReadOnlySpan<float> precipFieldFeburary = fields.Precipitation02.Values;
                ReadOnlySpan<float> precipFieldMarch = fields.Precipitation03.Values;
                ReadOnlySpan<float> precipFieldApril = fields.Precipitation04.Values;
                ReadOnlySpan<float> precipFieldMay = fields.Precipitation05.Values;
                ReadOnlySpan<float> precipFieldJune = fields.Precipitation06.Values;
                ReadOnlySpan<float> precipFieldJuly = fields.Precipitation07.Values;
                ReadOnlySpan<float> precipFieldAugust = fields.Precipitation08.Values;
                ReadOnlySpan<float> precipFieldSeptember = fields.Precipitation09.Values;
                ReadOnlySpan<float> precipFieldOctober = fields.Precipitation10.Values;
                ReadOnlySpan<float> precipFieldNovember = fields.Precipitation11.Values;
                ReadOnlySpan<float> precipFieldDecember = fields.Precipitation12.Values;

                ReadOnlySpan<float> snowFieldJanuary = fields.Snow01.Values;
                ReadOnlySpan<float> snowFieldFeburary = fields.Snow02.Values;
                ReadOnlySpan<float> snowFieldMarch = fields.Snow03.Values;
                ReadOnlySpan<float> snowFieldApril = fields.Snow04.Values;
                ReadOnlySpan<float> snowFieldMay = fields.Snow05.Values;
                ReadOnlySpan<float> snowFieldJune = fields.Snow06.Values;
                ReadOnlySpan<float> snowFieldJuly = fields.Snow07.Values;
                ReadOnlySpan<float> snowFieldAugust = fields.Snow08.Values;
                ReadOnlySpan<float> snowFieldSeptember = fields.Snow09.Values;
                ReadOnlySpan<float> snowFieldOctober = fields.Snow10.Values;
                ReadOnlySpan<float> snowFieldNovember = fields.Snow11.Values;
                ReadOnlySpan<float> snowFieldDecember = fields.Snow12.Values;

                ReadOnlySpan<float> solarRadiationFieldJanuary = fields.SolarRadiation01.Values;
                ReadOnlySpan<float> solarRadiationFieldFeburary = fields.SolarRadiation02.Values;
                ReadOnlySpan<float> solarRadiationFieldMarch = fields.SolarRadiation03.Values;
                ReadOnlySpan<float> solarRadiationFieldApril = fields.SolarRadiation04.Values;
                ReadOnlySpan<float> solarRadiationFieldMay = fields.SolarRadiation05.Values;
                ReadOnlySpan<float> solarRadiationFieldJune = fields.SolarRadiation06.Values;
                ReadOnlySpan<float> solarRadiationFieldJuly = fields.SolarRadiation07.Values;
                ReadOnlySpan<float> solarRadiationFieldAugust = fields.SolarRadiation08.Values;
                ReadOnlySpan<float> solarRadiationFieldSeptember = fields.SolarRadiation09.Values;
                ReadOnlySpan<float> solarRadiationFieldOctober = fields.SolarRadiation10.Values;
                ReadOnlySpan<float> solarRadiationFieldNovember = fields.SolarRadiation11.Values;
                ReadOnlySpan<float> solarRadiationFieldDecember = fields.SolarRadiation12.Values;

                ReadOnlySpan<float> temperatureMaxFieldJanuary = fields.TemperatureMax01.Values;
                ReadOnlySpan<float> temperatureMaxFieldFeburary = fields.TemperatureMax02.Values;
                ReadOnlySpan<float> temperatureMaxFieldMarch = fields.TemperatureMax03.Values;
                ReadOnlySpan<float> temperatureMaxFieldApril = fields.TemperatureMax04.Values;
                ReadOnlySpan<float> temperatureMaxFieldMay = fields.TemperatureMax05.Values;
                ReadOnlySpan<float> temperatureMaxFieldJune = fields.TemperatureMax06.Values;
                ReadOnlySpan<float> temperatureMaxFieldJuly = fields.TemperatureMax07.Values;
                ReadOnlySpan<float> temperatureMaxFieldAugust = fields.TemperatureMax08.Values;
                ReadOnlySpan<float> temperatureMaxFieldSeptember = fields.TemperatureMax09.Values;
                ReadOnlySpan<float> temperatureMaxFieldOctober = fields.TemperatureMax10.Values;
                ReadOnlySpan<float> temperatureMaxFieldNovember = fields.TemperatureMax11.Values;
                ReadOnlySpan<float> temperatureMaxFieldDecember = fields.TemperatureMax12.Values;

                ReadOnlySpan<float> temperatureMinFieldJanuary = fields.TemperatureMin01.Values;
                ReadOnlySpan<float> temperatureMinFieldFeburary = fields.TemperatureMin02.Values;
                ReadOnlySpan<float> temperatureMinFieldMarch = fields.TemperatureMin03.Values;
                ReadOnlySpan<float> temperatureMinFieldApril = fields.TemperatureMin04.Values;
                ReadOnlySpan<float> temperatureMinFieldMay = fields.TemperatureMin05.Values;
                ReadOnlySpan<float> temperatureMinFieldJune = fields.TemperatureMin06.Values;
                ReadOnlySpan<float> temperatureMinFieldJuly = fields.TemperatureMin07.Values;
                ReadOnlySpan<float> temperatureMinFieldAugust = fields.TemperatureMin08.Values;
                ReadOnlySpan<float> temperatureMinFieldSeptember = fields.TemperatureMin09.Values;
                ReadOnlySpan<float> temperatureMinFieldOctober = fields.TemperatureMin10.Values;
                ReadOnlySpan<float> temperatureMinFieldNovember = fields.TemperatureMin11.Values;
                ReadOnlySpan<float> temperatureMinFieldDecember = fields.TemperatureMin12.Values;

                ReadOnlySpan<float> temperatureMeanFieldJanuary = fields.TemperatureMean01.Values;
                ReadOnlySpan<float> temperatureMeanFieldFeburary = fields.TemperatureMean02.Values;
                ReadOnlySpan<float> temperatureMeanFieldMarch = fields.TemperatureMean03.Values;
                ReadOnlySpan<float> temperatureMeanFieldApril = fields.TemperatureMean04.Values;
                ReadOnlySpan<float> temperatureMeanFieldMay = fields.TemperatureMean05.Values;
                ReadOnlySpan<float> temperatureMeanFieldJune = fields.TemperatureMean06.Values;
                ReadOnlySpan<float> temperatureMeanFieldJuly = fields.TemperatureMean07.Values;
                ReadOnlySpan<float> temperatureMeanFieldAugust = fields.TemperatureMean08.Values;
                ReadOnlySpan<float> temperatureMeanFieldSeptember = fields.TemperatureMean09.Values;
                ReadOnlySpan<float> temperatureMeanFieldOctober = fields.TemperatureMean10.Values;
                ReadOnlySpan<float> temperatureMeanFieldNovember = fields.TemperatureMean11.Values;
                ReadOnlySpan<float> temperatureMeanFieldDecember = fields.TemperatureMean12.Values;

                ReadOnlySpan<float> relativeHumidityMeanFieldJanuary = fields.RelativeHumidityMean01.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldFeburary = fields.RelativeHumidityMean02.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldMarch = fields.RelativeHumidityMean03.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldApril = fields.RelativeHumidityMean04.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldMay = fields.RelativeHumidityMean05.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldJune = fields.RelativeHumidityMean06.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldJuly = fields.RelativeHumidityMean07.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldAugust = fields.RelativeHumidityMean08.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldSeptember = fields.RelativeHumidityMean09.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldOctober = fields.RelativeHumidityMean10.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldNovember = fields.RelativeHumidityMean11.Values;
                ReadOnlySpan<float> relativeHumidityMeanFieldDecember = fields.RelativeHumidityMean12.Values;

                string? currentWeatherID = null;
                for (int sourceIndex = 0; sourceIndex < batch.Length; ++sourceIndex /* destinationIndex incremented in loop */)
                {
                    Int16 year = yearField[sourceIndex];
                    if (year < startYear)
                    {
                        continue;
                    }

                    string weatherID = fields.ID.GetString(sourceIndex); // no ReadOnlySpan<char> API in Apache 9.0.0 or in .NET 6.0 System.Text.Encoding
                    if (String.Equals(weatherID, currentWeatherID, StringComparison.Ordinal) == false)
                    {
                        currentWeatherID = weatherID;
                        if (this.MonthlyWeatherByID.TryGetValue(weatherID, out monthlyWeather) == false)
                        {
                            // don't number of series in batch isn't known until last row, so can't estimate series length a priori
                            monthlyWeather = new(Timestep.Monthly);
                            this.MonthlyWeatherByID.Add(weatherID, monthlyWeather);
                        }
                    }
                    if (monthlyWeather!.Capacity - 12 < monthlyWeather.Count)
                    {
                        int estimatedCapacity = WeatherReaderMonthly.EstimateTimeSeriesLength(monthlyWeather, longestTimeSeriesLengthInMonths);
                        monthlyWeather.Resize(estimatedCapacity);
                    }

                    // gather and copy year, month, precipitation, snow, solar radiation, and maximum and minimum temperatures
                    int januaryDestinationIndex = monthlyWeather.Count;
                    monthlyWeather.Year.AsSpan().Slice(januaryDestinationIndex, Constant.Time.MonthsInYear).Fill(year);
                    monthOfYear.CopyTo(monthlyWeather.Month.AsSpan()[januaryDestinationIndex..]);

                    precipitationTotalByMonthInMM[0] = precipFieldJanuary[sourceIndex];
                    precipitationTotalByMonthInMM[1] = precipFieldFeburary[sourceIndex];
                    precipitationTotalByMonthInMM[2] = precipFieldMarch[sourceIndex];
                    precipitationTotalByMonthInMM[3] = precipFieldApril[sourceIndex];
                    precipitationTotalByMonthInMM[4] = precipFieldMay[sourceIndex];
                    precipitationTotalByMonthInMM[5] = precipFieldJune[sourceIndex];
                    precipitationTotalByMonthInMM[6] = precipFieldJuly[sourceIndex];
                    precipitationTotalByMonthInMM[7] = precipFieldAugust[sourceIndex];
                    precipitationTotalByMonthInMM[8] = precipFieldSeptember[sourceIndex];
                    precipitationTotalByMonthInMM[9] = precipFieldOctober[sourceIndex];
                    precipitationTotalByMonthInMM[10] = precipFieldNovember[sourceIndex];
                    precipitationTotalByMonthInMM[11] = precipFieldDecember[sourceIndex];
                    precipitationTotalByMonthInMM.CopyTo(monthlyWeather.PrecipitationTotalInMM.AsSpan()[januaryDestinationIndex..]);

                    snowTotalByMonthInMM[0] = snowFieldJanuary[sourceIndex];
                    snowTotalByMonthInMM[1] = snowFieldFeburary[sourceIndex];
                    snowTotalByMonthInMM[2] = snowFieldMarch[sourceIndex];
                    snowTotalByMonthInMM[3] = snowFieldApril[sourceIndex];
                    snowTotalByMonthInMM[4] = snowFieldMay[sourceIndex];
                    snowTotalByMonthInMM[5] = snowFieldJune[sourceIndex];
                    snowTotalByMonthInMM[6] = snowFieldJuly[sourceIndex];
                    snowTotalByMonthInMM[7] = snowFieldAugust[sourceIndex];
                    snowTotalByMonthInMM[8] = snowFieldSeptember[sourceIndex];
                    snowTotalByMonthInMM[9] = snowFieldOctober[sourceIndex];
                    snowTotalByMonthInMM[10] = snowFieldNovember[sourceIndex];
                    snowTotalByMonthInMM[11] = snowFieldDecember[sourceIndex];
                    snowTotalByMonthInMM.CopyTo(monthlyWeather.SnowTotalInMM.AsSpan()[januaryDestinationIndex..]);

                    solarRadiationTotalByMonth[0] = solarRadiationFieldJanuary[sourceIndex];
                    solarRadiationTotalByMonth[1] = solarRadiationFieldFeburary[sourceIndex];
                    solarRadiationTotalByMonth[2] = solarRadiationFieldMarch[sourceIndex];
                    solarRadiationTotalByMonth[3] = solarRadiationFieldApril[sourceIndex];
                    solarRadiationTotalByMonth[4] = solarRadiationFieldMay[sourceIndex];
                    solarRadiationTotalByMonth[5] = solarRadiationFieldJune[sourceIndex];
                    solarRadiationTotalByMonth[6] = solarRadiationFieldJuly[sourceIndex];
                    solarRadiationTotalByMonth[7] = solarRadiationFieldAugust[sourceIndex];
                    solarRadiationTotalByMonth[8] = solarRadiationFieldSeptember[sourceIndex];
                    solarRadiationTotalByMonth[9] = solarRadiationFieldOctober[sourceIndex];
                    solarRadiationTotalByMonth[10] = solarRadiationFieldNovember[sourceIndex];
                    solarRadiationTotalByMonth[11] = solarRadiationFieldDecember[sourceIndex];
                    solarRadiationTotalByMonth.CopyTo(monthlyWeather.SolarRadiationTotal.AsSpan()[januaryDestinationIndex..]);

                    temperatureMaxByMonth[0] = temperatureMaxFieldJanuary[sourceIndex];
                    temperatureMaxByMonth[1] = temperatureMaxFieldFeburary[sourceIndex];
                    temperatureMaxByMonth[2] = temperatureMaxFieldMarch[sourceIndex];
                    temperatureMaxByMonth[3] = temperatureMaxFieldApril[sourceIndex];
                    temperatureMaxByMonth[4] = temperatureMaxFieldMay[sourceIndex];
                    temperatureMaxByMonth[5] = temperatureMaxFieldJune[sourceIndex];
                    temperatureMaxByMonth[6] = temperatureMaxFieldJuly[sourceIndex];
                    temperatureMaxByMonth[7] = temperatureMaxFieldAugust[sourceIndex];
                    temperatureMaxByMonth[8] = temperatureMaxFieldSeptember[sourceIndex];
                    temperatureMaxByMonth[9] = temperatureMaxFieldOctober[sourceIndex];
                    temperatureMaxByMonth[10] = temperatureMaxFieldNovember[sourceIndex];
                    temperatureMaxByMonth[11] = temperatureMaxFieldDecember[sourceIndex];
                    temperatureMaxByMonth.CopyTo(monthlyWeather.TemperatureMax.AsSpan()[januaryDestinationIndex..]);

                    temperatureMinByMonth[0] = temperatureMinFieldJanuary[sourceIndex];
                    temperatureMinByMonth[1] = temperatureMinFieldFeburary[sourceIndex];
                    temperatureMinByMonth[2] = temperatureMinFieldMarch[sourceIndex];
                    temperatureMinByMonth[3] = temperatureMinFieldApril[sourceIndex];
                    temperatureMinByMonth[4] = temperatureMinFieldMay[sourceIndex];
                    temperatureMinByMonth[5] = temperatureMinFieldJune[sourceIndex];
                    temperatureMinByMonth[6] = temperatureMinFieldJuly[sourceIndex];
                    temperatureMinByMonth[7] = temperatureMinFieldAugust[sourceIndex];
                    temperatureMinByMonth[8] = temperatureMinFieldSeptember[sourceIndex];
                    temperatureMinByMonth[9] = temperatureMinFieldOctober[sourceIndex];
                    temperatureMinByMonth[10] = temperatureMinFieldNovember[sourceIndex];
                    temperatureMinByMonth[11] = temperatureMinFieldDecember[sourceIndex];
                    temperatureMinByMonth.CopyTo(monthlyWeather.TemperatureMin.AsSpan()[januaryDestinationIndex..]);

                    // estimate daytime mean temperature and vapor pressure deficit
                    temperatureMeanByMonth[0] = temperatureMeanFieldJanuary[sourceIndex];
                    temperatureMeanByMonth[1] = temperatureMeanFieldFeburary[sourceIndex];
                    temperatureMeanByMonth[2] = temperatureMeanFieldMarch[sourceIndex];
                    temperatureMeanByMonth[3] = temperatureMeanFieldApril[sourceIndex];
                    temperatureMeanByMonth[4] = temperatureMeanFieldMay[sourceIndex];
                    temperatureMeanByMonth[5] = temperatureMeanFieldJune[sourceIndex];
                    temperatureMeanByMonth[6] = temperatureMeanFieldJuly[sourceIndex];
                    temperatureMeanByMonth[7] = temperatureMeanFieldAugust[sourceIndex];
                    temperatureMeanByMonth[8] = temperatureMeanFieldSeptember[sourceIndex];
                    temperatureMeanByMonth[9] = temperatureMeanFieldOctober[sourceIndex];
                    temperatureMeanByMonth[10] = temperatureMeanFieldNovember[sourceIndex];
                    temperatureMeanByMonth[11] = temperatureMeanFieldDecember[sourceIndex];

                    WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(temperatureMinByMonth, temperatureMeanByMonth, temperatureMaxByMonth, temperatureDaytimeMeanByMonth);
                    temperatureDaytimeMeanByMonth.CopyTo(monthlyWeather.TemperatureDaytimeMean.AsSpan()[januaryDestinationIndex..]);

                    relativeHumidityMeanByMonth[0] = relativeHumidityMeanFieldJanuary[sourceIndex];
                    relativeHumidityMeanByMonth[1] = relativeHumidityMeanFieldFeburary[sourceIndex];
                    relativeHumidityMeanByMonth[2] = relativeHumidityMeanFieldMarch[sourceIndex];
                    relativeHumidityMeanByMonth[3] = relativeHumidityMeanFieldApril[sourceIndex];
                    relativeHumidityMeanByMonth[4] = relativeHumidityMeanFieldMay[sourceIndex];
                    relativeHumidityMeanByMonth[5] = relativeHumidityMeanFieldJune[sourceIndex];
                    relativeHumidityMeanByMonth[6] = relativeHumidityMeanFieldJuly[sourceIndex];
                    relativeHumidityMeanByMonth[7] = relativeHumidityMeanFieldAugust[sourceIndex];
                    relativeHumidityMeanByMonth[8] = relativeHumidityMeanFieldSeptember[sourceIndex];
                    relativeHumidityMeanByMonth[9] = relativeHumidityMeanFieldOctober[sourceIndex];
                    relativeHumidityMeanByMonth[10] = relativeHumidityMeanFieldNovember[sourceIndex];
                    relativeHumidityMeanByMonth[11] = relativeHumidityMeanFieldDecember[sourceIndex];

                    WeatherReaderMonthly.EstimateVaporPressureDeficit(temperatureMinByMonth, temperatureMeanByMonth, temperatureMaxByMonth, relativeHumidityMeanByMonth, vaporPressureDeficitMeanInKPa);
                    vaporPressureDeficitMeanInKPa.CopyTo(monthlyWeather.VpdMeanInKPa.AsSpan()[januaryDestinationIndex..]);

                    // complete year
                    monthlyWeather.Validate(monthlyWeather.Count, Constant.Time.MonthsInYear);

                    monthlyWeather.Count += 12;
                    if (monthlyWeather.Count > longestTimeSeriesLengthInMonths)
                    {
                        longestTimeSeriesLengthInMonths = monthlyWeather.Count;
                    }
                }
            }
        }
    }
}
