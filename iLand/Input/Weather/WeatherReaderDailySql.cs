using iLand.Input.ProjectFile;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace iLand.Input.Weather
{
    internal class WeatherReaderDailySql
    {
        private readonly float precipitationMultiplier; // daily precipitation scaling factor
        private int nextYearToLoad;
        private readonly int startYear;
        private readonly float temperatureShift; // offset of daily temperatures
        private readonly float temperatureTau;
        private readonly string weatherDatabaseFilePath;
        private readonly string weatherTableName; // database table to load this daily weather time series from

        public WeatherReaderDailySql(string weatherDatabaseFilePath, string weatherTableName, Project projectFile)
        {
            if (string.IsNullOrEmpty(weatherTableName))
            {
                throw new ArgumentOutOfRangeException(nameof(weatherTableName));
            }

            this.nextYearToLoad = 0;
            this.precipitationMultiplier = projectFile.World.Weather.PrecipitationMultiplier;
            this.startYear = projectFile.World.Weather.StartYear;
            this.temperatureShift = projectFile.World.Weather.TemperatureShift;
            this.temperatureTau = projectFile.Model.Ecosystem.TemperatureMA1tau;
            this.weatherDatabaseFilePath = weatherDatabaseFilePath;
            this.weatherTableName = weatherTableName;
        }

        public void LoadGroupOfYears(int yearsToLoad, WeatherTimeSeriesDaily dailyWeather, List<int> monthDayIndices)
        {
            string? weatherTableQueryFilter = null;
            if (this.startYear != Constant.NoDataInt32)
            {
                weatherTableQueryFilter = "where year >= " + this.startYear;
            }
            if (this.nextYearToLoad > 0)
            {
                if (String.IsNullOrWhiteSpace(weatherTableQueryFilter) == false)
                {
                    throw new NotImplementedException("Adjustment of weather query filter to load additional blocks of data is not currently implemented.");
                }
                // weatherTableQueryFilter = "where year > " + ?;
                // this.mCurrentDataYear = this.mNextYearToLoad;
                throw new NotImplementedException("Tracking of years loaded is not currently implemented. Consider specifying a larger weather load size as a workaround.");
            }
            string queryString = "select year,month,day,min_temp,max_temp,prec,rad,vpd from " + this.weatherTableName + " " + weatherTableQueryFilter + " order by year, month, day";

            // capture previous moving average, if available
            float previousMeanDaytimeTemperatureMA1 = Single.NaN;
            if (dailyWeather.Count > 0)
            {
                previousMeanDaytimeTemperatureMA1 = dailyWeather.TemperatureDaytimeMeanMA1[dailyWeather.Count - 1];
            }

            using SqliteConnection weatherDatabase = Landscape.GetDatabaseConnection(weatherDatabaseFilePath, openReadOnly: true);
            using SqliteCommand queryCommand = new(queryString, weatherDatabase);
            using SqliteDataReader weatherReader = queryCommand.ExecuteReader();

            int dayIndex = 0;
            int previousMonth = -1;
            int previousYear = -1;
            bool daysAvailableInQuery = true;
            dailyWeather.Count = 0;
            monthDayIndices.Clear();
            for (int yearLoadIndex = 0; daysAvailableInQuery && (yearLoadIndex < yearsToLoad); ++yearLoadIndex)
            {
                // check for year-specific temperature or precipitation modifier
                float precipitationMultiplier = this.precipitationMultiplier;
                float temperatureShift = this.temperatureShift;
                // TODO: reenable support for temperature shifts and precipitation multipliers for sensitivity analysis
                //if (model.ScheduledEvents != null)
                //{
                //    string temperatureAdditionAsString = model.ScheduledEvents.GetEvent(model.CurrentYear + yearLoadIndex, "/project/model/world/weather/temperatureShift");
                //    string precipitationMultiplierAsString = model.ScheduledEvents.GetEvent(model.CurrentYear + yearLoadIndex, "/project/model/world/weather/precipitationShift");
                //    if (temperatureAdditionAsString != null)
                //    {
                //        temperatureAddition = Single.Parse(temperatureAdditionAsString, CultureInfo.InvariantCulture);
                //    }
                //    if (precipitationMultiplierAsString != null)
                //    {
                //        precipitationMultiplier = Single.Parse(precipitationMultiplierAsString, CultureInfo.InvariantCulture);
                //    }

                //    if (temperatureAddition != 0.0 || precipitationMultiplier != 1.0)
                //    {
                //        Debug.WriteLine("Weather modification: temperature change " + temperatureAddition + "C. Precipitation multiplier: " + precipitationMultiplier);
                //        if (mDoRandomSampling)
                //        {
                //            Trace.TraceWarning("Weather: using a randomSamplingList and a temperature shift or precipitation multiplier at the same time. The same offset is applied for *every instance* of a year!!");
                //            //throw new NotSupportedException("Weather: cannot use a randomSamplingList and temperatureShift/precipitationShift at the same time. Sorry.");
                //        }
                //    }
                //}

                for (int daysLoaded = 0; daysAvailableInQuery = weatherReader.Read(); ++dayIndex) // mStore.begin();
                {
                    ++daysLoaded;
                    if (daysLoaded > Constant.DaysInLeapYear)
                    {
                        throw new NotSupportedException("Error in reading daily weather file: attempt to read more than " + Constant.DaysInLeapYear + " days in year.");
                    }

                    if (dailyWeather.Count == dailyWeather.Capacity)
                    {
                        dailyWeather.Resize(dailyWeather.Capacity + Constant.DaysInDecade);
                    }

                    int year = weatherReader.GetInt32(0);
                    int month = weatherReader.GetInt32(1);
                    int dayOfMonth = weatherReader.GetInt32(2);
                    float minTemperature = weatherReader.GetFloat(3) + temperatureShift;
                    float maxTemperature = weatherReader.GetFloat(4) + temperatureShift;

                    dailyWeather.Year[dayIndex] = year;
                    dailyWeather.Month[dayIndex] = month;
                    dailyWeather.DayOfMonth[dayIndex] = dayOfMonth;
                    dailyWeather.TemperatureMin[dayIndex] = minTemperature;
                    dailyWeather.TemperatureMax[dayIndex] = maxTemperature;
                    // References for calculation of daytime mean temperature:
                    //   Floyd, R. B., Braddock, R. D. 1984. A simple method for fitting average diurnal temperature curves.  Agricultural and Forest Meteorology 32: 107-119.
                    //   Landsberg, J. J. 1986. Physiological ecology of forest production. Academic Press Inc., 197 S.
                    // For the primary meteorology stations as a group on the HJ Andrews Research Forest this form has adjusted R² = 0.98 with a mean absolute error
                    // of 1.20 C *but* the coefficient is 1.044 rather than 0.212.
                    float meanTemperature = 0.5F * (minTemperature + maxTemperature);
                    dailyWeather.TemperatureDaytimeMean[dayIndex] = 0.212F * (maxTemperature - meanTemperature) + meanTemperature;
                    dailyWeather.PrecipitationTotalInMM[dayIndex] = weatherReader.GetFloat(5) * precipitationMultiplier;
                    dailyWeather.SolarRadiationTotal[dayIndex] = weatherReader.GetFloat(6);
                    dailyWeather.VpdMeanInKPa[dayIndex] = weatherReader.GetFloat(7);
                    ++dailyWeather.Count;
                    dailyWeather.Validate(dayIndex);

                    if (month != previousMonth)
                    {
                        // new month...
                        previousMonth = month;
                        // save relative position of the beginning of the new month
                        monthDayIndices.Add(dayIndex);
                    }
                    if (daysLoaded == 1)
                    {
                        // check on first day of the year
                        if (previousYear != -1 && year != previousYear + 1)
                        {
                            throw new NotSupportedException(String.Format("Error in reading daily weather file: invalid year break at year-month-dday: {0}-{1}-{2}!", year, month, dayOfMonth));
                        }
                    }

                    previousYear = year;
                    if ((month == Constant.MonthsInYear) && (dayOfMonth == 31)) // check is specific to December, so 31 days
                    {
                        // increment day insert point since break statement skips this inner loop's increment
                        // Prevents the next iteration of the outer loop from overwriting the last day of the year.
                        ++dayIndex;
                        break;
                    }
                }
            }

            monthDayIndices.Add(dayIndex); // the absolute last day...
            this.nextYearToLoad += yearsToLoad;

            // first order dynamic delayed model of Mäkelä 2008
            // handle first day: use tissue temperature of the last day of the previous year if available
            dailyWeather.TemperatureDaytimeMeanMA1[0] = dailyWeather.TemperatureDaytimeMean[0];
            if (Single.IsNaN(previousMeanDaytimeTemperatureMA1) == false)
            {
                dailyWeather.TemperatureDaytimeMeanMA1[0] = previousMeanDaytimeTemperatureMA1 + 1.0F / this.temperatureTau * (dailyWeather.TemperatureDaytimeMean[0] - previousMeanDaytimeTemperatureMA1);
            }
            for (int ma1index = 1; ma1index < dailyWeather.Count; ++ma1index)
            {
                dailyWeather.TemperatureDaytimeMeanMA1[ma1index] = dailyWeather.TemperatureDaytimeMeanMA1[ma1index - 1] + 1.0F / this.temperatureTau * (dailyWeather.TemperatureDaytimeMean[ma1index] - dailyWeather.TemperatureDaytimeMeanMA1[ma1index - 1]);
            }
        }
    }
}
