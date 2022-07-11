using Apache.Arrow;
using Apache.Arrow.Ipc;
using iLand.Tool;
using System;
using System.IO;
using System.Linq;

namespace iLand.Input
{
    internal class WeatherReaderMonthlyFeather : WeatherReaderMonthly
    {
        public WeatherReaderMonthlyFeather(string weatherFilePath)
        {
            // only works with uncompressed feather files in Arrow 8.0.0: https://issues.apache.org/jira/browse/ARROW-17062
            using FileStream weatherStream = new(weatherFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, Constant.File.DefaultBufferSize);
            using ArrowFileReader weatherFile = new(weatherStream); // ArrowFileReader.IsFileValid is false until a batch is read

            for (RecordBatch batch = weatherFile.ReadNextRecordBatch(); batch != null; batch = weatherFile.ReadNextRecordBatch())
            {
                IArrowArray[] fields = batch.Arrays.ToArray();

                WeatherDataIndexMonthly fieldIndices = new(batch);
                Int32Array year = (Int32Array)fields[fieldIndices.Year];
                StringArray weatherIDs = (StringArray)fields[fieldIndices.ID];
                FloatArray precipJanuary = (FloatArray)fields[fieldIndices.Precipitation01];
                FloatArray precipFebruary = (FloatArray)fields[fieldIndices.Precipitation02];
                FloatArray precipMarch = (FloatArray)fields[fieldIndices.Precipitation03];
                FloatArray precipApril = (FloatArray)fields[fieldIndices.Precipitation04];
                FloatArray precipMay = (FloatArray)fields[fieldIndices.Precipitation05];
                FloatArray precipJune = (FloatArray)fields[fieldIndices.Precipitation06];
                FloatArray precipJuly = (FloatArray)fields[fieldIndices.Precipitation07];
                FloatArray precipAugust = (FloatArray)fields[fieldIndices.Precipitation08];
                FloatArray precipSeptember = (FloatArray)fields[fieldIndices.Precipitation09];
                FloatArray precipOctober = (FloatArray)fields[fieldIndices.Precipitation10];
                FloatArray precipNovember = (FloatArray)fields[fieldIndices.Precipitation11];
                FloatArray precipDecember = (FloatArray)fields[fieldIndices.Precipitation12];
                FloatArray snowJanuary = (FloatArray)fields[fieldIndices.Snow01];
                FloatArray snowFebruary = (FloatArray)fields[fieldIndices.Snow02];
                FloatArray snowMarch = (FloatArray)fields[fieldIndices.Snow03];
                FloatArray snowApril = (FloatArray)fields[fieldIndices.Snow04];
                FloatArray snowMay = (FloatArray)fields[fieldIndices.Snow05];
                FloatArray snowJune = (FloatArray)fields[fieldIndices.Snow06];
                FloatArray snowJuly = (FloatArray)fields[fieldIndices.Snow07];
                FloatArray snowAugust = (FloatArray)fields[fieldIndices.Snow08];
                FloatArray snowSeptember = (FloatArray)fields[fieldIndices.Snow09];
                FloatArray snowOctober = (FloatArray)fields[fieldIndices.Snow10];
                FloatArray snowNovember = (FloatArray)fields[fieldIndices.Snow11];
                FloatArray snowDecember = (FloatArray)fields[fieldIndices.Snow12];
                FloatArray solarRadiationJanuary = (FloatArray)fields[fieldIndices.SolarRadiation01];
                FloatArray solarRadiationFebruary = (FloatArray)fields[fieldIndices.SolarRadiation02];
                FloatArray solarRadiationMarch = (FloatArray)fields[fieldIndices.SolarRadiation03];
                FloatArray solarRadiationApril = (FloatArray)fields[fieldIndices.SolarRadiation04];
                FloatArray solarRadiationMay = (FloatArray)fields[fieldIndices.SolarRadiation05];
                FloatArray solarRadiationJune = (FloatArray)fields[fieldIndices.SolarRadiation06];
                FloatArray solarRadiationJuly = (FloatArray)fields[fieldIndices.SolarRadiation07];
                FloatArray solarRadiationAugust = (FloatArray)fields[fieldIndices.SolarRadiation08];
                FloatArray solarRadiationSeptember = (FloatArray)fields[fieldIndices.SolarRadiation09];
                FloatArray solarRadiationOctober = (FloatArray)fields[fieldIndices.SolarRadiation10];
                FloatArray solarRadiationNovember = (FloatArray)fields[fieldIndices.SolarRadiation11];
                FloatArray solarRadiationDecember = (FloatArray)fields[fieldIndices.SolarRadiation12];
                FloatArray tempMinJanuary = (FloatArray)fields[fieldIndices.TemperatureMin01];
                FloatArray tempMinFebruary = (FloatArray)fields[fieldIndices.TemperatureMin02];
                FloatArray tempMinMarch = (FloatArray)fields[fieldIndices.TemperatureMin03];
                FloatArray tempMinApril = (FloatArray)fields[fieldIndices.TemperatureMin04];
                FloatArray tempMinMay = (FloatArray)fields[fieldIndices.TemperatureMin05];
                FloatArray tempMinJune = (FloatArray)fields[fieldIndices.TemperatureMin06];
                FloatArray tempMinJuly = (FloatArray)fields[fieldIndices.TemperatureMin07];
                FloatArray tempMinAugust = (FloatArray)fields[fieldIndices.TemperatureMin08];
                FloatArray tempMinSeptember = (FloatArray)fields[fieldIndices.TemperatureMin09];
                FloatArray tempMinOctober = (FloatArray)fields[fieldIndices.TemperatureMin10];
                FloatArray tempMinNovember = (FloatArray)fields[fieldIndices.TemperatureMin11];
                FloatArray tempMinDecember = (FloatArray)fields[fieldIndices.TemperatureMin12];
                FloatArray tempMeanJanuary = (FloatArray)fields[fieldIndices.TemperatureMean01];
                FloatArray tempMeanFebruary = (FloatArray)fields[fieldIndices.TemperatureMean02];
                FloatArray tempMeanMarch = (FloatArray)fields[fieldIndices.TemperatureMean03];
                FloatArray tempMeanApril = (FloatArray)fields[fieldIndices.TemperatureMean04];
                FloatArray tempMeanMay = (FloatArray)fields[fieldIndices.TemperatureMean05];
                FloatArray tempMeanJune = (FloatArray)fields[fieldIndices.TemperatureMean06];
                FloatArray tempMeanJuly = (FloatArray)fields[fieldIndices.TemperatureMean07];
                FloatArray tempMeanAugust = (FloatArray)fields[fieldIndices.TemperatureMean08];
                FloatArray tempMeanSeptember = (FloatArray)fields[fieldIndices.TemperatureMean09];
                FloatArray tempMeanOctober = (FloatArray)fields[fieldIndices.TemperatureMean10];
                FloatArray tempMeanNovember = (FloatArray)fields[fieldIndices.TemperatureMean11];
                FloatArray tempMeanDecember = (FloatArray)fields[fieldIndices.TemperatureMean12];
                FloatArray tempMaxJanuary = (FloatArray)fields[fieldIndices.TemperatureMax01];
                FloatArray tempMaxFebruary = (FloatArray)fields[fieldIndices.TemperatureMax02];
                FloatArray tempMaxMarch = (FloatArray)fields[fieldIndices.TemperatureMax03];
                FloatArray tempMaxApril = (FloatArray)fields[fieldIndices.TemperatureMax04];
                FloatArray tempMaxMay = (FloatArray)fields[fieldIndices.TemperatureMax05];
                FloatArray tempMaxJune = (FloatArray)fields[fieldIndices.TemperatureMax06];
                FloatArray tempMaxJuly = (FloatArray)fields[fieldIndices.TemperatureMax07];
                FloatArray tempMaxAugust = (FloatArray)fields[fieldIndices.TemperatureMax08];
                FloatArray tempMaxSeptember = (FloatArray)fields[fieldIndices.TemperatureMax09];
                FloatArray tempMaxOctober = (FloatArray)fields[fieldIndices.TemperatureMax10];
                FloatArray tempMaxNovember = (FloatArray)fields[fieldIndices.TemperatureMax11];
                FloatArray tempMaxDecember = (FloatArray)fields[fieldIndices.TemperatureMax12];

                WeatherTimeSeriesMonthly? monthlyWeather = null;
                string? previousWeatherID = null;
                for (int sourceIndex = 0; sourceIndex < batch.Length; ++sourceIndex /* destinationIndex in loop */)
                {
                    string weatherID = weatherIDs.GetString(sourceIndex);
                    if (String.Equals(weatherID, previousWeatherID) == false)
                    {
                        if (this.MonthlyWeatherByID.TryGetValue(weatherID, out monthlyWeather) == false)
                        {
                            // don't number of series in batch isn't known until last row, so can't estimate series length a priori
                            monthlyWeather = new WeatherTimeSeriesMonthly(Timestep.Monthly, Constant.Data.MonthlyWeatherAllocationIncrement);
                            this.MonthlyWeatherByID.Add(weatherID, monthlyWeather);
                        }

                        previousWeatherID = weatherID;
                    }
                    if (monthlyWeather!.Capacity - 12 < monthlyWeather.Count)
                    {
                        monthlyWeather.Resize(monthlyWeather.Capacity + Constant.Data.MonthlyWeatherAllocationIncrement);
                    }

                    // January
                    int destinationIndex = monthlyWeather.Count;
                    monthlyWeather.Month[destinationIndex] = 1;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipJanuary.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowJanuary.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationJanuary.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanJanuary.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxJanuary.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinJanuary.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // February
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 2;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipFebruary.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowFebruary.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationFebruary.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanFebruary.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxFebruary.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinFebruary.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // March
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 3;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipMarch.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowMarch.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationMarch.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanMarch.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxMarch.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinMarch.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // April
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 4;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipApril.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowApril.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationApril.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanApril.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxApril.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinApril.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // May
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 5;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipMay.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowMay.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationMay.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanMay.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxMay.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinMay.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // June
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 6;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipJune.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowJune.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationJune.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanJune.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxJune.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinJune.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // July
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 7;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipJuly.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowJuly.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationJuly.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanJuly.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxJuly.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinJuly.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // August
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 8;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipAugust.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowAugust.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationAugust.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanAugust.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxAugust.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinAugust.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // September
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 9;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipSeptember.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowSeptember.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationSeptember.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanSeptember.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxSeptember.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinSeptember.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // October
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 10;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipOctober.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowOctober.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationOctober.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanOctober.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxOctober.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinOctober.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // November
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 11;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipNovember.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowNovember.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationNovember.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanNovember.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxNovember.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinNovember.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    // December
                    ++destinationIndex;
                    monthlyWeather.Month[destinationIndex] = 12;
                    monthlyWeather.PrecipitationTotalInMM[destinationIndex] = precipDecember.Values[sourceIndex];
                    monthlyWeather.SnowTotalInMM[destinationIndex] = snowDecember.Values[sourceIndex];
                    monthlyWeather.SolarRadiationTotal[destinationIndex] = solarRadiationDecember.Values[sourceIndex];
                    monthlyWeather.TemperatureDaytimeMean[destinationIndex] = tempMeanDecember.Values[sourceIndex];
                    monthlyWeather.TemperatureMax[destinationIndex] = tempMaxDecember.Values[sourceIndex];
                    monthlyWeather.TemperatureMin[destinationIndex] = tempMinDecember.Values[sourceIndex];
                    // monthlyWeather.VpdMeanInKPa[destinationIndex] = 
                    monthlyWeather.Year[destinationIndex] = year.Values[sourceIndex];
                    monthlyWeather.Validate(destinationIndex);

                    monthlyWeather.Count += 12;
                }
            }
        }
    }
}
