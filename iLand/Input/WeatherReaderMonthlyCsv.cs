using iLand.Input.ProjectFile;
using iLand.Tool;
using System;

namespace iLand.Input
{
    internal class WeatherReaderMonthlyCsv : WeatherReaderMonthly
    {
        public WeatherReaderMonthlyCsv(Project projectFile)
            : this(projectFile.GetFilePath(ProjectDirectory.Database, projectFile.World.Weather.File))
        {
        }

        public WeatherReaderMonthlyCsv(string weatherFilePath)
        {
            // TODO: support for temperature shifts and precipitation multipliers for sensitivity analysis
            CsvFile weatherFile = new(weatherFilePath);
            WeatherDataIndexMonthly weatherHeader = new(weatherFile);

            weatherFile.Parse((string[] row) =>
            {
                string weatherID = row[weatherHeader.ID];
                if (this.MonthlyWeatherByID.TryGetValue(weatherID, out WeatherTimeSeriesMonthly? monthlyWeather) == false)
                {
                    monthlyWeather = new(Timestep.Monthly, Constant.Data.MonthlyWeatherAllocationIncrement);
                    this.MonthlyWeatherByID.Add(weatherID, monthlyWeather);
                }
                else if (monthlyWeather.Capacity - 12 < monthlyWeather.Count)
                {
                    monthlyWeather.Resize(monthlyWeather.Capacity + Constant.Data.MonthlyWeatherAllocationIncrement);
                }

                // TODO: calculate VPD
                int year = Int32.Parse(row[weatherHeader.Year]);
                // January
                int monthIndex = monthlyWeather.Count;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 1;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation01]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow01]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation01]);
                float maxTemp = Single.Parse(row[weatherHeader.TemperatureMax01]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                float minTemp = Single.Parse(row[weatherHeader.TemperatureMin01]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                float meanTemp = Single.Parse(row[weatherHeader.TemperatureMean01]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean01]));
                monthlyWeather.Validate(monthIndex);
                // February
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 2;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation02]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow02]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation02]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax02]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin02]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean02]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean02]));
                monthlyWeather.Validate(monthIndex);
                // March
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 3;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation03]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow03]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation03]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax03]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin03]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean03]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean03]));
                monthlyWeather.Validate(monthIndex);
                // April
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 4;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation04]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow04]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation04]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax04]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin04]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean04]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean04]));
                monthlyWeather.Validate(monthIndex);
                // May
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 5;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation05]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow05]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation05]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax05]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin05]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean05]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean05]));
                monthlyWeather.Validate(monthIndex);
                // June
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 6;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation06]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow06]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation06]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax06]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin06]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean06]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean06]));
                monthlyWeather.Validate(monthIndex);
                // July
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 7;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation07]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow07]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation07]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax07]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin07]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean07]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean07]));
                monthlyWeather.Validate(monthIndex);
                // August
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 8;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation08]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow08]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation08]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax08]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin08]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean08]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean08]));
                monthlyWeather.Validate(monthIndex);
                // September
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 9;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation09]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow09]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation09]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax09]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin09]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean09]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean09]));
                monthlyWeather.Validate(monthIndex);
                // October
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 10;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation10]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow10]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation10]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax10]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin10]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean10]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean10]));
                monthlyWeather.Validate(monthIndex);
                // Novemeber
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 11;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation11]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow11]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation11]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax11]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin11]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean11]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean11]));
                monthlyWeather.Validate(monthIndex);
                // Decemeber
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 12;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation12]);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow12]);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation12]);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax12]);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin12]);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean12]);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean12]));
                monthlyWeather.Validate(monthIndex);

                monthlyWeather.Count += 12;
            });
        }
    }
}
