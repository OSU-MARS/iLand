using iLand.Input.ProjectFile;
using System;
using System.Globalization;

namespace iLand.Input
{
    internal class WeatherReaderMonthlyCsv : WeatherReaderMonthly
    {
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
                int year = Int32.Parse(row[weatherHeader.Year], CultureInfo.InvariantCulture);
                // January
                int monthIndex = monthlyWeather.Count;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 1;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation01], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow01], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation01], CultureInfo.InvariantCulture);
                float maxTemp = Single.Parse(row[weatherHeader.TemperatureMax01], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                float minTemp = Single.Parse(row[weatherHeader.TemperatureMin01], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                float meanTemp = Single.Parse(row[weatherHeader.TemperatureMean01], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean01], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // February
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 2;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation02], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow02], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation02], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax02], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin02], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean02], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean02], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // March
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 3;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation03], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow03], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation03], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax03], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin03], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean03], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean03], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // April
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 4;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation04], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow04], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation04], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax04], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin04], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean04], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean04], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // May
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 5;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation05], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow05], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation05], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax05], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin05], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean05], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean05], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // June
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 6;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation06], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow06], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation06], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax06], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin06], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean06], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean06], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // July
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 7;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation07], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow07], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation07], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax07], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin07], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean07], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean07], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // August
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 8;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation08], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow08], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation08], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax08], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin08], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean08], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean08], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // September
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 9;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation09], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow09], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation09], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax09], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin09], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean09], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean09], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // October
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 10;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation10], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow10], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation10], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax10], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin10], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean10], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean10], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // Novemeber
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 11;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation11], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow11], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation11], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax11], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin11], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean11], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean11], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);
                // Decemeber
                ++monthIndex;
                monthlyWeather.Year[monthIndex] = year;
                monthlyWeather.Month[monthIndex] = 12;
                monthlyWeather.PrecipitationTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Precipitation12], CultureInfo.InvariantCulture);
                monthlyWeather.SnowTotalInMM[monthIndex] = Single.Parse(row[weatherHeader.Snow12], CultureInfo.InvariantCulture);
                monthlyWeather.SolarRadiationTotal[monthIndex] = Single.Parse(row[weatherHeader.SolarRadiation12], CultureInfo.InvariantCulture);
                maxTemp = Single.Parse(row[weatherHeader.TemperatureMax12], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMax[monthIndex] = maxTemp;
                minTemp = Single.Parse(row[weatherHeader.TemperatureMin12], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureMin[monthIndex] = minTemp;
                meanTemp = Single.Parse(row[weatherHeader.TemperatureMean12], CultureInfo.InvariantCulture);
                monthlyWeather.TemperatureDaytimeMean[monthIndex] = WeatherReaderMonthly.EstimateDaytimeMeanAirTemperature(minTemp, meanTemp, maxTemp);
                monthlyWeather.VpdMeanInKPa[monthIndex] = WeatherReaderMonthly.EstimateVaporPressureDeficit(minTemp, meanTemp, maxTemp, Single.Parse(row[weatherHeader.RelativeHumidityMean12], CultureInfo.InvariantCulture));
                monthlyWeather.Validate(monthIndex);

                monthlyWeather.Count += 12;
            });
        }
    }
}
