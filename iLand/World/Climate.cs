using iLand.Input.ProjectFile;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Model = iLand.Simulation.Model;

namespace iLand.World
{
    // Climate handles climate input data and performs some basic related calculations on that data.
    // http://iland.boku.ac.at/ClimateData
    public class Climate
    {
        private string? climateTableQueryFilter;
        private int mCurrentDataYear; // current year in climate data cached in memory (relative); one less than GlobalSettings.CurrentYear
        private int mNextYearToLoad; // start year of climate data cached in memory
        private float mDefaultTemperatureAddition; // add this to daily temp
        private float mDefaultPrecipitationMultiplier; // multiply prec with that
        private readonly List<ClimateDay> mDays; // storage of climate data
        private bool mDoRandomSampling; // if true, the sequence of years is randomized
        private readonly List<int> mMonthDayIndices; // store indices for month / years within store
        private readonly List<Phenology> mPhenology; // phenology calculations
        private readonly List<int> mRandomYearList; // for random sampling of years
        private int mRandomListIndex; // current index of the randomYearList for random sampling
        private readonly List<int> mSampledYears; // list of sampled years to use
        private int mYearsToLoad; // number of years to load from database

        public int CurrentJanuary1 { get; private set; } // STL-like (pointer)-iterator  to the first day of the current year
        public int NextJanuary1 { get; private set; } // STL-like pointer iterator to the day *after* last day of the current year
        public float CarbonDioxidePpm { get; private set; }
        public bool IsSetup { get; private set; }
        /// the mean annual temperature of the current year (degree C)
        public float MeanAnnualTemperature { get; private set; }
        public string Name { get; init; } // table name of this climate
        public float[] PrecipitationByMonth { get; init; }
        // access to other subsystems
        public Sun Sun { get; init; } // solar radiation class
        // get a array with mean temperatures per month (deg C)
        public float[] TemperatureByMonth { get; init; }
        public float TotalAnnualRadiation { get; private set; } // return radiation sum (MJ) of the whole year

        public Climate(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentOutOfRangeException(nameof(name));
            }

            this.mCurrentDataYear = 0;
            this.mDays = new List<ClimateDay>(Constant.DaysInLeapYear); // one year minimum capacity
            this.mNextYearToLoad = 0;
            this.mMonthDayIndices = new List<int>(13); // one year minimum capacity
            this.mPhenology = new List<Phenology>();
            this.mRandomYearList = new List<int>();
            this.mSampledYears = new List<int>();

            // initialized in Setup()
            // this.mDefaultPrecipitationMultiplier
            // this.mDefaultTemperatureAddition;
            // this.mDoRandomSampling;
            // this.mYearsToLoad;

            // initialized in Load();
            // this.NextJanuary1;
            // initialized in NextYear();
            // this.TotalAnnualRadiation

            this.Name = name;
            this.PrecipitationByMonth = new float[Constant.MonthsInYear];
            this.Sun = new Sun();
            this.TemperatureByMonth = new float[Constant.MonthsInYear];
        }

        public ClimateDay this[int index]
        {
            get { return this.mDays[index]; }
        }

        public float GetDayLengthInHours(int day) { return this.Sun.GetDayLengthInHours(day); } // length of the day in hours
        // access to climate data
        public int GetIndexOfDayInCurrentYear(int dayOfYear) { return CurrentJanuary1 + dayOfYear; } // get pointer to climate structure by day of year (0-based-index)
        public int GetDayOfCurrentYear(int dayIndex) { return dayIndex - CurrentJanuary1; } // get the 0-based index of the climate given by 'climate' within the current year

        /// annual precipitation sum (mm)
        public float GetTotalPrecipitationInCurrentYear() 
        { 
            float totalPrecip = 0.0F;
            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                totalPrecip += this.PrecipitationByMonth[month];
            }
            return totalPrecip; 
        }

        // gets mStore index of climate structure of given day (0-based indices, i.e. month=11=december!)
        public int IndexOf(int month, int day)
        {
            return mMonthDayIndices[mCurrentDataYear * Constant.MonthsInYear + month] + day;
        }

        // returns number of days of given month (0..11)
        public int GetDaysInMonth(int month)
        {
            return mMonthDayIndices[mCurrentDataYear * Constant.MonthsInYear + month + 1] - mMonthDayIndices[mCurrentDataYear * Constant.MonthsInYear + month];
        }

        // returns number of days of current year.
        public int GetDaysInYear()
        {
            Debug.Assert(this.NextJanuary1 > this.CurrentJanuary1);
            return this.NextJanuary1 - this.CurrentJanuary1;
        }

        // load mLoadYears years from database
        private void LoadYear(Project projectFile)
        {
            string? climateTableQueryFilter = null;
            if (String.IsNullOrEmpty(this.climateTableQueryFilter) == false)
            {
                climateTableQueryFilter = "where " + this.climateTableQueryFilter;
            }
            if (this.mNextYearToLoad > 0)
            {
                if (String.IsNullOrWhiteSpace(this.climateTableQueryFilter) == false)
                {
                    throw new NotImplementedException("Adjustment of climate query filter to load additional blocks of data is not currently implemented.");
                }
                // climateTableQueryFilter = "where year > " + ?;
                // this.mCurrentDataYear = this.mNextYearToLoad;
                throw new NotImplementedException("Tracking of years loaded is not currently implemented. Consider specifying a larger climate batch size as a workaround.");
            }
            string query = String.Format("select year,month,day,min_temp,max_temp,prec,rad,vpd from {0} {1} order by year, month, day", Name, climateTableQueryFilter);

            string climateDatabaseFilePath = projectFile.GetFilePath(ProjectDirectory.Database, projectFile.World.Climate.DatabaseFile);
            using SqliteConnection climateDatabase = Landscape.GetDatabaseConnection(climateDatabaseFilePath, true);
            using SqliteCommand queryCommand = new SqliteCommand(query, climateDatabase);
            using SqliteDataReader climateReader = queryCommand.ExecuteReader();

            int dayIndex = 0;
            int previousMonth = -1;
            int previousYear = -1;
            bool daysAvailableInQuery = true;
            this.mMonthDayIndices.Clear();
            for (int yearLoadIndex = 0; daysAvailableInQuery && (yearLoadIndex < mYearsToLoad); ++yearLoadIndex)
            {
                // check for year-specific temperature or precipitation modifier
                float precipitationMultiplier = mDefaultPrecipitationMultiplier;
                float temperatureAddition = mDefaultTemperatureAddition;
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

                for (int daysLoadedInYear = 0; daysAvailableInQuery = climateReader.Read(); ++dayIndex) // mStore.begin();
                {
                    ++daysLoadedInYear;
                    if (daysLoadedInYear > Constant.DaysInLeapYear)
                    {
                        throw new NotSupportedException("Error in reading climate file: attempt to read more than " + Constant.DaysInLeapYear + " days in year.");
                    }

                    ClimateDay day;
                    if (this.mDays.Count <= dayIndex)
                    {
                        day = new ClimateDay();
                        mDays.Add(day);
                    }
                    else
                    {
                        day = mDays[dayIndex];
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
                        throw new SqliteException(String.Format("Invalid dates in climate table {0}: year {1} month {2} day {3}!", Name, day.Year, day.Month, day.DayOfMonth), (int)SqliteErrorCode.DataTypeMismatch);
                    }
                    Debug.WriteLineIf(day.Month < 1 || day.DayOfMonth < 1 || day.Month > Constant.MonthsInYear || day.DayOfMonth > 31, "Climate:load", "invalid dates");
                    Debug.WriteLineIf(day.MeanDaytimeTemperature < -70 || day.MeanDaytimeTemperature > 50, "Climate:load", "temperature out of range (-70..+50 degree C)");
                    Debug.WriteLineIf(day.Preciptitation < 0 || day.Preciptitation > 200, "Climate:load", "precipitation out of range (0..200mm)");
                    Debug.WriteLineIf(day.Radiation < 0 || day.Radiation > 50, "Climate:load", "radiation out of range (0..50 MJ/m2/day)");
                    Debug.WriteLineIf(day.Vpd < 0 || day.Vpd > 10, "Climate:load", "vpd out of range (0..10 kPa)");

                    if (day.Month != previousMonth)
                    {
                        // new month...
                        previousMonth = day.Month;
                        // save relative position of the beginning of the new month
                        mMonthDayIndices.Add(dayIndex);
                    }
                    if (daysLoadedInYear == 1)
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

            this.mMonthDayIndices.Add(dayIndex); // the absolute last day...
            this.mNextYearToLoad += this.mYearsToLoad;

            this.CurrentJanuary1 = this.mMonthDayIndices[Constant.MonthsInYear * this.mCurrentDataYear];
            this.NextJanuary1 = this.mMonthDayIndices[Constant.MonthsInYear * (this.mCurrentDataYear + 1)]; // point to 1 January of the next year
            
            int lastDay = this.IndexOf(11, 30); // 31 December in zero based indexing
            ClimateDay lastDayOfYear = mDays[lastDay];
            float tau = projectFile.Model.Ecosystem.TemperatureTau;
            // handle first day: use tissue temperature of the last day of the last year (if available)
            this.mDays[0].TempDelayed = lastDayOfYear.TempDelayed + 1.0F / tau * (mDays[0].MeanDaytimeTemperature - lastDayOfYear.TempDelayed);

            for (int dayIndex2 = 1; dayIndex2 < this.mDays.Count; ++dayIndex2)
            {
                // first order dynamic delayed model (Mäkelä 2008)
                this.mDays[dayIndex2].TempDelayed = mDays[dayIndex2 - 1].TempDelayed + 1.0F / tau * (mDays[dayIndex2].MeanDaytimeTemperature - mDays[dayIndex2 - 1].TempDelayed);
            }
        }

        // returns two pointer (arguments!!!) to the begin and one after end of the given month (month: 0..11)
        public void GetMonthIndices(int zeroBasedMonth, out int firstDayInMonthIndex, out int firstDayInNextMonthIndex)
        {
            firstDayInMonthIndex = mMonthDayIndices[mCurrentDataYear * Constant.MonthsInYear + zeroBasedMonth];
            firstDayInNextMonthIndex = mMonthDayIndices[mCurrentDataYear * Constant.MonthsInYear + zeroBasedMonth + 1];
            //Debug.WriteLine("monthRange returning: begin:"+ (*rBegin).toString() + "end-1:" + (*rEnd-1).toString();
        }

        // activity
        public void OnStartYear(Model model)
        {
            if (mDoRandomSampling == false)
            {
                // default behaviour: simply advance to next year, call load() if end reached
                if (mCurrentDataYear >= mYearsToLoad - 1) // need to load more data
                {
                    this.LoadYear(model.Project);
                }
                else
                {
                    ++this.mCurrentDataYear;
                }
            }
            else
            {
                // random sampling
                if (mRandomYearList.Count == 0)
                {
                    // random without list
                    // make sure that the sequence of years is the same for the full landscape
                    if (mSampledYears.Count < model.CurrentYear)
                    {
                        while (mSampledYears.Count - 1 < model.CurrentYear)
                        {
                            mSampledYears.Add(model.RandomGenerator.GetRandomInteger(0, mYearsToLoad));
                        }
                    }

                    mCurrentDataYear = mSampledYears[model.CurrentYear];
                }
                else
                {
                    // random with fixed list
                    ++mRandomListIndex;
                    if (mRandomListIndex >= mRandomYearList.Count)
                    {
                        mRandomListIndex = 0;
                    }
                    mCurrentDataYear = mRandomYearList[mRandomListIndex];
                    if (mCurrentDataYear >= mYearsToLoad)
                    {
                        throw new NotSupportedException(String.Format("Climate: load year with random sampling: the actual year {0} is invalid. Only {1} years are loaded from the climate database.", mCurrentDataYear, mYearsToLoad));
                    }
                }
                if (model.Project.Output.Logging.LogLevel <= EventLevel.Informational)
                {
                    Trace.TraceInformation("Climate: current year (randomized): " + mCurrentDataYear);
                }
            }

            this.CarbonDioxidePpm = model.Project.World.Climate.CO2ConcentrationInPpm;
            if (model.Project.Output.Logging.LogLevel <= EventLevel.Informational)
            {
                Trace.TraceInformation("CO2 concentration " + this.CarbonDioxidePpm + " ppm.");
            }
            int currentJanuary1dayIndex = Constant.MonthsInYear * mCurrentDataYear;
            int nextJanuary1dayIndex = currentJanuary1dayIndex + Constant.MonthsInYear;
            if ((currentJanuary1dayIndex > mMonthDayIndices.Count) || (nextJanuary1dayIndex > mMonthDayIndices.Count))
            {
                throw new NotSupportedException("Climate data is not available for simulation year " + mCurrentDataYear + ".");
            }
            this.CurrentJanuary1 = mMonthDayIndices[mCurrentDataYear * Constant.MonthsInYear];
            this.NextJanuary1 = mMonthDayIndices[(mCurrentDataYear + 1) * Constant.MonthsInYear];

            // some aggregates:
            // calculate radiation sum of the year and monthly precipitation
            this.TotalAnnualRadiation = 0.0F;
            this.MeanAnnualTemperature = 0.0F;
            for (int monthIndex = 0; monthIndex < Constant.MonthsInYear; ++monthIndex)
            {
                this.PrecipitationByMonth[monthIndex] = 0.0F;
                this.TemperatureByMonth[monthIndex] = 0.0F;
            }

            for (int dayIndex = this.CurrentJanuary1; dayIndex < this.NextJanuary1; ++dayIndex)
            {
                ClimateDay day = mDays[dayIndex];
                this.TotalAnnualRadiation += day.Radiation;
                this.MeanAnnualTemperature += day.MeanDaytimeTemperature;
                this.PrecipitationByMonth[day.Month - 1] += day.Preciptitation;
                this.TemperatureByMonth[day.Month - 1] += day.MeanDaytimeTemperature;
            }
            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                this.TemperatureByMonth[month] /= this.GetDaysInMonth(month);
            }
            this.MeanAnnualTemperature /= this.GetDaysInYear();

            // calculate phenology
            for (int index = 0; index < this.mPhenology.Count; ++index)
            {
                this.mPhenology[index].RunYear();
            }
        }

        // phenology class of given type
        public Phenology GetPhenology(int phenologyIndex)
        {
            if (phenologyIndex >= mPhenology.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(phenologyIndex), "Phenology group " + phenologyIndex + "not present. Is /project/model/species/phenology missing elements?");
            }

            Phenology phenology = mPhenology[phenologyIndex];
            if (phenology.LeafType == phenologyIndex)
            {
                return phenology;
            }

            // search...
            for (int index = 0; index < mPhenology.Count; index++)
            {
                if (mPhenology[index].LeafType == phenologyIndex)
                {
                    return mPhenology[index];
                }
            }
            throw new ArgumentOutOfRangeException(nameof(phenologyIndex), String.Format("Error at SpeciesSet::phenology(): invalid group: {0}", phenologyIndex));
        }

        // setup routine that opens database connection
        public void Setup(Project projectFile)
        {
            this.climateTableQueryFilter = projectFile.World.Climate.DatabaseQueryFilter;

            this.mYearsToLoad = projectFile.World.Climate.BatchYears;
            this.mDoRandomSampling = projectFile.World.Climate.RandomSamplingEnabled;
            this.mRandomYearList.Clear();
            this.mRandomListIndex = -1;
            if (this.mDoRandomSampling)
            {
                string? list = projectFile.World.Climate.RandomSamplingList;
                if (String.IsNullOrEmpty(list) == false)
                {
                    List<string> strlist = Regex.Split(list, "\\W+").ToList();
                    foreach (string s in strlist)
                    {
                        this.mRandomYearList.Add(Int32.Parse(s));
                    }
                    // check for validity
                    foreach (int year in this.mRandomYearList)
                    {
                        if (year < 0 || year >= mYearsToLoad)
                        {
                            throw new NotSupportedException("Invalid randomSamplingList! Year numbers are 0-based and must to between 0 and batchYears-1 (check value of batchYears)!!!");
                        }
                    }
                }

                if (mRandomYearList.Count > 0)
                {
                    Debug.WriteLine("Climate: Random sampling enabled with fixed list " + mRandomYearList.Count + " of years. climate: " + Name);
                }
                else
                {
                    Debug.WriteLine("Climate: Random sampling enabled (without a fixed list). climate: " + Name);
                }
            }
            mDefaultTemperatureAddition = projectFile.World.Climate.TemperatureShift;
            this.mDefaultPrecipitationMultiplier = projectFile.World.Climate.PrecipitationMultiplier;
            if (this.mDefaultTemperatureAddition != 0.0F || this.mDefaultPrecipitationMultiplier != 1.0F)
            {
                Debug.WriteLine("Climate modification: add temperature: " + mDefaultTemperatureAddition + ". Multiply precipitation: " + mDefaultPrecipitationMultiplier);
            }

            this.mCurrentDataYear = 0;
            this.mNextYearToLoad = 0;

            // setup query
            // load first chunk...
            this.LoadYear(projectFile);
            this.SetupPhenology(projectFile);
            this.Sun.Setup(Maths.ToRadians(projectFile.World.Geometry.Latitude));
            this.mCurrentDataYear = -1; // go to "-1" -> the first call to next year will go to year 0.
            this.mSampledYears.Clear();
            this.IsSetup = true;
        }

        // setup of phenology groups
        private void SetupPhenology(Project project)
        {
            this.mPhenology.Clear();
            this.mPhenology.Add(new Phenology(this)); // id=0

            // TODO: remove PhenologyType and make Phenology XML serializable
            foreach (PhenologyType phenology in project.World.Species.Phenology)
            {
                if (phenology.ID < 0)
                {
                    throw new XmlException("Invalid leaf type ID " + phenology.ID + ".");
                }
                Phenology item = new Phenology(phenology.ID, 
                                               this, 
                                               phenology.VpdMin,
                                               phenology.VpdMax,
                                               phenology.DayLengthMin,
                                               phenology.DayLengthMax,
                                               phenology.TempMin,
                                               phenology.TempMax);
                mPhenology.Add(item);
            } 
        }

        // decode "yearday" to the actual year, month, day if provided
        public void ToZeroBasedDate(int dayOfYear, out int zeroBasedDay, out int zeroBasedMonth, out int year)
        {
            ClimateDay day = mDays[this.GetIndexOfDayInCurrentYear(dayOfYear)];
            zeroBasedDay = day.DayOfMonth - 1;
            zeroBasedMonth = day.Month - 1;
            year = day.Year;
        }
    }
}
