using iLand.Input.ProjectFile;
using iLand.Tool;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace iLand.Input
{
    internal class ClimateReader
    {
        private readonly string? climateTableQueryFilter;
        private int nextYearToLoad;
        private readonly float defaultTemperatureAddition; // offset of daily temp
        private readonly float mDefaultPrecipitationMultiplier; // daily precipitation scaling factor

        public string ClimateTableName { get; private init; } // database table to load this climate from

        public ClimateReader(Project projectFile, string name)
        {
            this.climateTableQueryFilter = projectFile.World.Climate.DatabaseQueryFilter;
            this.defaultTemperatureAddition = projectFile.World.Climate.TemperatureShift;
            this.mDefaultPrecipitationMultiplier = projectFile.World.Climate.PrecipitationMultiplier;
            this.nextYearToLoad = 0;

            this.ClimateTableName = name;
        }

        public void LoadGroupOfYears(Project projectFile, int yearsToLoad, List<ClimateDay> climateDays, List<int> monthDayIndices)
        {
            string? climateTableQueryFilter = null;
            if (String.IsNullOrEmpty(this.climateTableQueryFilter) == false)
            {
                climateTableQueryFilter = "where " + this.climateTableQueryFilter;
            }
            if (this.nextYearToLoad > 0)
            {
                if (String.IsNullOrWhiteSpace(climateTableQueryFilter) == false)
                {
                    throw new NotImplementedException("Adjustment of climate query filter to load additional blocks of data is not currently implemented.");
                }
                // climateTableQueryFilter = "where year > " + ?;
                // this.mCurrentDataYear = this.mNextYearToLoad;
                throw new NotImplementedException("Tracking of years loaded is not currently implemented. Consider specifying a larger climate batch size as a workaround.");
            }
            string query = "select year,month,day,min_temp,max_temp,prec,rad,vpd from " + this.ClimateTableName + " " + climateTableQueryFilter + " order by year, month, day";

            // if available, retain last day of previous
            ClimateDay? lastDayOfPreviousYear = null;
            if (climateDays.Count > 0)
            {
                lastDayOfPreviousYear = climateDays[^1];
            }

            string climateDatabaseFilePath = projectFile.GetFilePath(ProjectDirectory.Database, projectFile.World.Climate.DatabaseFile);
            using SqliteConnection climateDatabase = Landscape.GetDatabaseConnection(climateDatabaseFilePath, openReadOnly: true);
            using SqliteCommand queryCommand = new(query, climateDatabase);
            using SqliteDataReader climateReader = queryCommand.ExecuteReader();

            int dayIndex = 0;
            int previousMonth = -1;
            int previousYear = -1;
            bool daysAvailableInQuery = true;
            monthDayIndices.Clear();
            for (int yearLoadIndex = 0; daysAvailableInQuery && (yearLoadIndex < yearsToLoad); ++yearLoadIndex)
            {
                // check for year-specific temperature or precipitation modifier
                float precipitationMultiplier = this.mDefaultPrecipitationMultiplier;
                float temperatureAddition = this.defaultTemperatureAddition;
                // TODO: reenable support for temperature shifts and precipitation multipliers for sensitivity analysis
                //if (model.ScheduledEvents != null)
                //{
                //    string temperatureAdditionAsString = model.ScheduledEvents.GetEvent(model.CurrentYear + yearLoadIndex, "model.climate.temperatureShift");
                //    string precipitationMultiplierAsString = model.ScheduledEvents.GetEvent(model.CurrentYear + yearLoadIndex, "model.climate.precipitationShift");
                //    if (temperatureAdditionAsString != null)
                //    {
                //        temperatureAddition = Single.Parse(temperatureAdditionAsString);
                //    }
                //    if (precipitationMultiplierAsString != null)
                //    {
                //        precipitationMultiplier = Single.Parse(precipitationMultiplierAsString);
                //    }

                //    if (temperatureAddition != 0.0 || precipitationMultiplier != 1.0)
                //    {
                //        Debug.WriteLine("Climate modification: temperature change " + temperatureAddition + "C. Precipitation multiplier: " + precipitationMultiplier);
                //        if (mDoRandomSampling)
                //        {
                //            Trace.TraceWarning("WARNING - Climate: using a randomSamplingList and a temperature shift or precipitation multiplier at the same time. The same offset is applied for *every instance* of a year!!");
                //            //throw new NotSupportedException("Climate: cannot use a randomSamplingList and temperatureShift/precipitationShift at the same time. Sorry.");
                //        }
                //    }
                //}

                for (int daysLoaded = 0; daysAvailableInQuery = climateReader.Read(); ++dayIndex) // mStore.begin();
                {
                    ++daysLoaded;
                    if (daysLoaded > Constant.DaysInLeapYear)
                    {
                        throw new NotSupportedException("Error in reading climate file: attempt to read more than " + Constant.DaysInLeapYear + " days in year.");
                    }

                    ClimateDay day;
                    if (climateDays.Count <= dayIndex)
                    {
                        day = new ClimateDay();
                        climateDays.Add(day);
                    }
                    else
                    {
                        day = climateDays[dayIndex];
                    }
                    day.Year = climateReader.GetInt32(0);
                    day.Month = climateReader.GetInt32(1);
                    day.DayOfMonth = climateReader.GetInt32(2);
                    day.MinTemperature = climateReader.GetFloat(3) + temperatureAddition;
                    day.MaxTemperature = climateReader.GetFloat(4) + temperatureAddition;
                    //References for calculation the temperature of the day:
                    //Floyd, R. B., Braddock, R. D. 1984. A simple method for fitting average diurnal temperature curves.  Agricultural and Forest Meteorology 32: 107-119.
                    //Landsberg, J. J. 1986. Physiological ecology of forest production. Academic Press Inc., 197 S.
                    day.MeanDaytimeTemperature = 0.212F * (day.MaxTemperature - day.MeanTemperature()) + day.MeanTemperature();
                    day.Preciptitation = climateReader.GetFloat(5) * precipitationMultiplier;
                    day.Radiation = climateReader.GetFloat(6);
                    day.Vpd = climateReader.GetFloat(7);
                    // sanity checks
                    if (day.Month < 1 || day.DayOfMonth < 1 || day.Month > Constant.MonthsInYear || day.DayOfMonth > DateTime.DaysInMonth(day.Year, day.Month))
                    {
                        throw new SqliteException(String.Format("Invalid dates in climate table {0}: year {1} month {2} day {3}!", this.ClimateTableName, day.Year, day.Month, day.DayOfMonth), (int)SqliteErrorCode.DataTypeMismatch);
                    }
                    // Debug.WriteLineIf(day.Month < 1 || day.DayOfMonth < 1 || day.Month > Constant.MonthsInYear || day.DayOfMonth > 31, "Climate:load", "invalid dates");
                    // Debug.WriteLineIf(day.MeanDaytimeTemperature < -70 || day.MeanDaytimeTemperature > 50, "Climate:load", "temperature out of range (-70..+50 degree C)");
                    // Debug.WriteLineIf(day.Preciptitation < 0 || day.Preciptitation > 200, "Climate:load", "precipitation out of range (0..200mm)");
                    // Debug.WriteLineIf(day.Radiation < 0 || day.Radiation > 50, "Climate:load", "radiation out of range (0..50 MJ/m2/day)");
                    // Debug.WriteLineIf(day.Vpd < 0 || day.Vpd > 10, "Climate:load", "vpd out of range (0..10 kPa)");

                    if (day.Month != previousMonth)
                    {
                        // new month...
                        previousMonth = day.Month;
                        // save relative position of the beginning of the new month
                        monthDayIndices.Add(dayIndex);
                    }
                    if (daysLoaded == 1)
                    {
                        // check on first day of the year
                        if (previousYear != -1 && day.Year != previousYear + 1)
                        {
                            throw new NotSupportedException(String.Format("Error in reading climate file: invalid year break at y-m-d: {0}-{1}-{2}!", day.Year, day.Month, day.DayOfMonth));
                        }
                    }

                    previousYear = day.Year;
                    if (day.Month == Constant.MonthsInYear && day.DayOfMonth == 31)
                    {
                        // increment day insert point since break statement skips this inner loop's increment
                        // Prevents the next iteration of the outer loop from overwriting the last day of the year.
                        ++dayIndex;
                        break;
                    }
                }
            }
            if (climateDays.Count > dayIndex)
            {
                // drop any days left over from a previous climate load
                // This is likely to happen in any case where multiple climate loads occur since the length of the database is probably not an exact 
                // multiple of the load size.
                climateDays.RemoveRange(dayIndex, climateDays.Count - dayIndex);
            }

            monthDayIndices.Add(dayIndex); // the absolute last day...
            this.nextYearToLoad += yearsToLoad;

            // first order dynamic delayed model of Mäkelä 2008
            // handle first day: use tissue temperature of the last day of the previous year if available
            float tau = projectFile.Model.Ecosystem.TemperatureAveragingTau;
            climateDays[0].MeanDaytimeTemperatureMA1 = climateDays[0].MeanDaytimeTemperature;
            if (lastDayOfPreviousYear != null)
            {
                climateDays[0].MeanDaytimeTemperatureMA1 = lastDayOfPreviousYear.MeanDaytimeTemperatureMA1 + 1.0F / tau * (climateDays[0].MeanDaytimeTemperature - lastDayOfPreviousYear.MeanDaytimeTemperatureMA1);
            }
            for (int averageIndex = 1; averageIndex < climateDays.Count; ++averageIndex)
            {
                climateDays[averageIndex].MeanDaytimeTemperatureMA1 = climateDays[averageIndex - 1].MeanDaytimeTemperatureMA1 + 1.0F / tau * (climateDays[averageIndex].MeanDaytimeTemperature - climateDays[averageIndex - 1].MeanDaytimeTemperatureMA1);
            }
        }
    }
}
