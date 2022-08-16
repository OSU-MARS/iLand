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
            // Arrow 8.0.0 supports only uncompressed feather: https://issues.apache.org/jira/browse/ARROW-17062
            using FileStream weatherStream = new(weatherFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize);
            using ArrowFileReader weatherFile = new(weatherStream); // ArrowFileReader.IsFileValid is false until a batch is read

            for (RecordBatch? batch = weatherFile.ReadNextRecordBatch(); batch != null; batch = weatherFile.ReadNextRecordBatch())
            {
                WeatherArrowBatchMonthly fields = new(batch);

                WeatherTimeSeriesMonthly? monthlyWeather = null;
                string? previousWeatherID = null;
                for (int sourceIndex = 0; sourceIndex < batch.Length; ++sourceIndex /* destinationIndex incremented in loop */)
                {
                    int year = fields.Year.Values[sourceIndex];
                    if (year < startYear)
                    {
                        continue;
                    }

                    string weatherID = fields.ID.GetString(sourceIndex);
                    if (String.Equals(weatherID, previousWeatherID) == false)
                    {
                        if (this.MonthlyWeatherByID.TryGetValue(weatherID, out monthlyWeather) == false)
                        {
                            // don't number of series in batch isn't known until last row, so can't estimate series length a priori
                            monthlyWeather = new(Timestep.Monthly, Constant.Data.MonthlyAllocationIncrement);
                            this.MonthlyWeatherByID.Add(weatherID, monthlyWeather);
                        }

                        previousWeatherID = weatherID;
                    }
                    if (monthlyWeather!.Capacity - 12 < monthlyWeather.Count)
                    {
                        monthlyWeather.Resize(monthlyWeather.Capacity + Constant.Data.MonthlyAllocationIncrement);
                    }

                    // January
                    int destinationIndex = monthlyWeather.Count;
                    monthlyWeather.Month[destinationIndex] = 1;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation01.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow01.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation01.Values[sourceIndex];
                    float maxTemp = fields.TemperatureMax01.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    float minTemp = fields.TemperatureMin01.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    float meanTemp = fields.TemperatureMean01.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean01.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // February
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 2;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation02.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow02.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation02.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax02.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin02.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean02.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean02.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // March
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 3;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation03.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow03.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation03.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax03.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin03.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean03.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean03.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // April
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 4;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation04.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow04.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation04.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax04.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin04.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean04.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean04.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // May
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 5;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation05.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow05.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation05.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax05.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin05.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean05.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean05.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // June
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 6;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation06.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow06.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation06.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax06.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin06.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean06.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean06.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // July
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 7;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation07.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow07.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation07.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax07.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin07.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean07.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean07.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // August
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 8;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation08.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow08.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation08.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax08.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin08.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean08.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean08.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // September
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 9;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation09.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow09.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation09.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax09.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin09.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean09.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean09.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // October
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 10;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation10.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow10.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation10.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax10.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin10.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean10.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean10.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // November
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 11;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation11.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow11.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation11.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax11.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin11.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean11.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean11.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // December
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 12;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = fields.Precipitation12.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = fields.Snow12.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = fields.SolarRadiation12.Values[sourceIndex];
                    maxTemp = fields.TemperatureMax12.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = maxTemp;
                    minTemp = fields.TemperatureMin12.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = minTemp;
                    meanTemp = fields.TemperatureMean12.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                    monthlyWeather.VpdMeanInKPa[destinationIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, fields.RelativeHumidityMean12.Values[sourceIndex]);
                    monthlyWeather.Year[destinationIndex] = fields.Year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    monthlyWeather.Count += 12;
                }
            }
        }
    }
}
