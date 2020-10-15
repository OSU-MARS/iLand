using iLand.Simulation;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace iLand.World
{
    // Climate handles climate input data and performs some basic related calculations on that data.
    // http://iland.boku.ac.at/ClimateData
    public class Climate
    {
        private string climateTableQueryFilter;
        private int mCurrentDataYear; // current year in climate data cached in memory (relative); one less than GlobalSettings.CurrentYear
        private int mNextYearToLoad; // start year of climate data cached in memory
        private double mDefaultTemperatureAddition; // add this to daily temp
        private double mDefaultPrecipitationMultiplier; // multiply prec with that
        private readonly List<ClimateDay> mDays; ///< storage of climate data
        private bool mDoRandomSampling; ///< if true, the sequence of years is randomized
        private readonly List<int> mMonthDayIndices; ///< store indices for month / years within store
        private readonly List<Phenology> mPhenology; ///< phenology calculations
        private readonly List<int> mRandomYearList; ///< for random sampling of years
        private int mRandomListIndex; ///< current index of the randomYearList for random sampling
        private readonly List<int> mSampledYears; // list of sampled years to use
        private int mYearsToLoad; // number of years to load from database

        public int CurrentJanuary1 { get; private set; } ///< STL-like (pointer)-iterator  to the first day of the current year
        public int NextJanuary1 { get; private set; } ///< STL-like pointer iterator to the day *after* last day of the current year
        public double CarbonDioxidePpm { get; private set; }
        public bool IsSetup { get; private set; }
        /// the mean annual temperature of the current year (degree C)
        public double MeanAnnualTemperature { get; private set; }
        public string Name { get; private set; } ///< table name of this climate
        public double[] PrecipitationByMonth { get; private set; }
        // access to other subsystems
        public Sun Sun { get; private set; } ///< solar radiation class
        // get a array with mean temperatures per month (deg C)
        public double[] TemperatureByMonth { get; private set; }
        public double TotalAnnualRadiation { get; private set; } ///< return radiation sum (MJ) of the whole year

        public Climate()
        {
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
            // this.Name;

            // initialized in Load();
            // this.NextJanuary1;
            // initialized in NextYear();
            // this.TotalAnnualRadiation

            this.PrecipitationByMonth = new double[12];
            this.Sun = new Sun();
            this.TemperatureByMonth = new double[12];
        }

        public ClimateDay this[int index]
        {
            get { return this.mDays[index]; }
        }

        /// annual precipitation sum (mm)
        public double AnnualPrecipitation() { double r = 0.0; for (int i = 0; i < 12; ++i) r += PrecipitationByMonth[i]; return r; }
        public double DayLengthInHours(int day) { return Sun.GetDaylength(day); } ///< length of the day in hours
        // access to climate data
        public int DayOfYear(int dayofyear) { return CurrentJanuary1 + dayofyear; } ///< get pointer to climate structure by day of year (0-based-index)
        public int WhichDayOfYear(int dayIndex) { return dayIndex - CurrentJanuary1; } ///< get the 0-based index of the climate given by 'climate' within the current year

        // more calculations done after loading of climate data
        private void ClimateCalculations(int lastIndex, Model model)
        {
            ClimateDay lastDayOfYear = mDays[lastIndex];
            double tau = model.ModelSettings.TemperatureTau;
            // handle first day: use tissue temperature of the last day of the last year (if available)
            if (lastDayOfYear.IsValid())
            {
                mDays[0].TempDelayed = lastDayOfYear.TempDelayed + 1.0 / tau * (mDays[0].MeanDaytimeTemperature - lastDayOfYear.TempDelayed);
            }
            else
            {
                mDays[0].TempDelayed = mDays[0].MeanDaytimeTemperature;
            }

            for (int c = 1; c < mDays.Count; ++c)
            {
                // first order dynamic delayed model (Maekela 2008)
                mDays[c].TempDelayed = mDays[c - 1].TempDelayed + 1.0 / tau * (mDays[c].MeanDaytimeTemperature - mDays[c - 1].TempDelayed);
            }
        }

        // gets mStore index of climate structure of given day (0-based indices, i.e. month=11=december!)
        public int IndexOf(int month, int day)
        {
            if (mMonthDayIndices.Count == 0)
            {
                return -1;
            }
            return mMonthDayIndices[mCurrentDataYear * 12 + month] + day;
        }

        // returns number of days of given month (0..11)
        public double Days(int month)
        {
            return (double)mMonthDayIndices[mCurrentDataYear * 12 + month + 1] - mMonthDayIndices[mCurrentDataYear * 12 + month];
        }

        // returns number of days of current year.
        public int DaysOfYear()
        {
            if (mMonthDayIndices.Count == 0)
            {
                return -1;
            }
            return NextJanuary1 - CurrentJanuary1;
        }

        // load mLoadYears years from database
        private void Load(Model model)
        {
            string climateTableQueryFilter = null;
            if (String.IsNullOrEmpty(this.climateTableQueryFilter) == false)
            {
                climateTableQueryFilter = "where " + this.climateTableQueryFilter;
            }
            else if (this.mNextYearToLoad > 0)
            {
                if (String.IsNullOrWhiteSpace(this.climateTableQueryFilter) == false)
                {
                    throw new NotImplementedException("Adjustment of climate query filter to load additional blocks of data is not currently implemented.");
                }
                // climateTableQueryFilter = "where year > " + ?;
                throw new NotImplementedException("Tracking of years loaded is not currently implemented. Consider specifying a larger climate batch size as a workaround.");
            }
            string query = String.Format("select year,month,day,min_temp,max_temp,prec,rad,vpd from {0} {1} order by year, month, day", Name, climateTableQueryFilter);
            using SqliteCommand queryCommand = new SqliteCommand(query, model.GlobalSettings.DatabaseClimate);
            using SqliteDataReader climateReader = queryCommand.ExecuteReader();

            int dayIndex = 0;
            int previousMonth = -1;
            int previousYear = -1;
            bool daysAvailableInQuery = true;
            mMonthDayIndices.Clear();
            for (int yearLoadIndex = 0; daysAvailableInQuery && (yearLoadIndex < mYearsToLoad); ++yearLoadIndex)
            {
                // check for year-specific temperature or precipitation modifier
                double precipitationMultiplier = mDefaultPrecipitationMultiplier;
                double temperatureAddition = mDefaultTemperatureAddition;
                if (model.TimeEvents != null)
                {
                    object val_temp = model.TimeEvents.Value(model.GlobalSettings.CurrentYear + yearLoadIndex, "model.climate.temperatureShift");
                    object val_prec = model.TimeEvents.Value(model.GlobalSettings.CurrentYear + yearLoadIndex, "model.climate.precipitationShift");
                    if (val_temp != null)
                    {
                        temperatureAddition = (double)val_temp;
                    }
                    if (val_prec != null)
                    {
                        precipitationMultiplier = (double)val_prec;
                    }

                    if (temperatureAddition != 0.0 || precipitationMultiplier != 1.0)
                    {
                        Debug.WriteLine("Climate modification: add temperature:" + temperatureAddition + ". Multiply precipitation: " + precipitationMultiplier);
                        if (mDoRandomSampling)
                        {
                            Trace.TraceWarning("WARNING - Climate: using a randomSamplingList and temperatureShift/precipitationShift at the same time. The same offset is applied for *every instance* of a year!!");
                            //throw new NotSupportedException("Climate: cannot use a randomSamplingList and temperatureShift/precipitationShift at the same time. Sorry.");
                        }
                    }
                }

                for (int daysLoadedInYear = 0; daysAvailableInQuery = climateReader.Read(); ++dayIndex) // mStore.begin();
                {
                    ++daysLoadedInYear;
                    if (daysLoadedInYear > Constant.DaysInLeapYear)
                    {
                        throw new NotSupportedException("Error in reading climate file: attempt to read more than " + Constant.DaysInLeapYear + " days in year.");
                    }

                    ClimateDay day;
                    if (mDays.Count <= dayIndex)
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
                    day.MinTemperature = climateReader.GetDouble(3) + temperatureAddition;
                    day.MaxTemperature = climateReader.GetDouble(4) + temperatureAddition;
                    //References for calculation the temperature of the day:
                    //Floyd, R. B., Braddock, R. D. 1984. A simple method for fitting average diurnal temperature curves.  Agricultural and Forest Meteorology 32: 107-119.
                    //Landsberg, J. J. 1986. Physiological ecology of forest production. Academic Press Inc., 197 S.
                    day.MeanDaytimeTemperature = 0.212 * (day.MaxTemperature - day.MeanTemperature()) + day.MeanTemperature();
                    day.Preciptitation = climateReader.GetDouble(5) * precipitationMultiplier;
                    day.Radiation = climateReader.GetDouble(6);
                    day.Vpd = climateReader.GetDouble(7);
                    // sanity checks
                    if (day.Month < 1 || day.DayOfMonth < 1 || day.Month > 12 || day.DayOfMonth > DateTime.DaysInMonth(day.Year, day.Month))
                    {
                        throw new SqliteException(String.Format("Invalid dates in climate table {0}: year {1} month {2} day {3}!", Name, day.Year, day.Month, day.DayOfMonth), -1);
                    }
                    Debug.WriteLineIf(day.Month < 1 || day.DayOfMonth < 1 || day.Month > 12 || day.DayOfMonth > 31, "Climate:load", "invalid dates");
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
                    if (day.Month == 12 && day.DayOfMonth == 31)
                    {
                        // increment day insert point since break statement skips this inner loop's increment
                        // Prevents the next iteration of the outer loop from overwriting the last day of the year.
                        ++dayIndex;
                        break;
                    }
                }
            }

            this.mMonthDayIndices.Add(dayIndex); // the absolute last day...
            this.mCurrentDataYear = 0;
            this.mNextYearToLoad += this.mYearsToLoad;

            this.CurrentJanuary1 = this.mMonthDayIndices[12 * this.mCurrentDataYear];
            this.NextJanuary1 = this.mMonthDayIndices[12 * (this.mCurrentDataYear + 1)]; // point to 1 January of the next year
            
            int lastDay = this.IndexOf(11, 30); // 31 December in zero based indexing
            this.ClimateCalculations(lastDay, model); // perform additional calculations based on the climate data loaded from the database
        }

        // returns two pointer (arguments!!!) to the begin and one after end of the given month (month: 0..11)
        public void MonthRange(int month, out int rBegin, out int rEnd)
        {
            rBegin = mMonthDayIndices[mCurrentDataYear * 12 + month];
            rEnd = mMonthDayIndices[mCurrentDataYear * 12 + month + 1];
            //Debug.WriteLine("monthRange returning: begin:"+ (*rBegin).toString() + "end-1:" + (*rEnd-1).toString();
        }

        // activity
        public void NextYear(Model model)
        {
            if (mDoRandomSampling == false)
            {
                // default behaviour: simply advance to next year, call load() if end reached
                if (mCurrentDataYear >= mYearsToLoad - 1) // need to load more data
                {
                    Load(model);
                }
                else
                {
                    mCurrentDataYear++;
                }
            }
            else
            {
                // random sampling
                if (mRandomYearList.Count == 0)
                {
                    // random without list
                    // make sure that the sequence of years is the same for the full landscape
                    if (mSampledYears.Count < model.GlobalSettings.CurrentYear)
                    {
                        while (mSampledYears.Count - 1 < model.GlobalSettings.CurrentYear)
                        {
                            mSampledYears.Add(model.RandomGenerator.Random(0, mYearsToLoad));
                        }
                    }

                    mCurrentDataYear = mSampledYears[model.GlobalSettings.CurrentYear];
                }
                else
                {
                    // random with fixed list
                    mRandomListIndex++;
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
                if (model.GlobalSettings.LogInfo())
                {
                    Debug.WriteLine("Climate: current year (randomized): " + mCurrentDataYear);
                }
            }

            this.CarbonDioxidePpm = model.GlobalSettings.Settings.GetDouble("model.climate.co2concentration", Constant.Default.CarbonDioxidePpm);
            if (model.GlobalSettings.LogInfo())
            {
                Debug.WriteLine("CO2 concentration " + this.CarbonDioxidePpm + " ppm.");
            }
            int currentJanuary1dayIndex = 12 * mCurrentDataYear;
            int nextJanuary1dayIndex = currentJanuary1dayIndex + 12;
            if ((currentJanuary1dayIndex > mMonthDayIndices.Count) || (nextJanuary1dayIndex > mMonthDayIndices.Count))
            {
                throw new NotSupportedException("Climate data is not available for simulation year " + mCurrentDataYear + ".");
            }
            CurrentJanuary1 = mMonthDayIndices[mCurrentDataYear * 12];
            NextJanuary1 = mMonthDayIndices[(mCurrentDataYear + 1) * 12];

            // some aggregates:
            // calculate radiation sum of the year and monthly precipitation
            TotalAnnualRadiation = 0.0;
            MeanAnnualTemperature = 0.0;
            for (int i = 0; i < 12; i++)
            {
                PrecipitationByMonth[i] = 0.0;
                TemperatureByMonth[i] = 0.0;
            }

            for (int dayIndex = CurrentJanuary1; dayIndex < NextJanuary1; ++dayIndex)
            {
                ClimateDay day = mDays[dayIndex];
                TotalAnnualRadiation += day.Radiation;
                MeanAnnualTemperature += day.MeanDaytimeTemperature;
                PrecipitationByMonth[day.Month - 1] += day.Preciptitation;
                TemperatureByMonth[day.Month - 1] += day.MeanDaytimeTemperature;
            }
            for (int month = 0; month < 12; ++month)
            {
                TemperatureByMonth[month] /= Days(month);
            }
            MeanAnnualTemperature /= DaysOfYear();

            // calculate phenology
            for (int i = 0; i < mPhenology.Count; ++i)
            {
                mPhenology[i].Calculate(model.GlobalSettings);
            }
        }

        // phenology class of given type
        public Phenology Phenology(int phenologyGroup)
        {
            if (phenologyGroup >= mPhenology.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(phenologyGroup), "Phenology group " + phenologyGroup + "not present. Is /project/model/species/phenology missing elements?");
            }

            Phenology p = mPhenology[phenologyGroup];
            if (p.ID == phenologyGroup)
            {
                return p;
            }

            // search...
            for (int i = 0; i < mPhenology.Count; i++)
            {
                if (mPhenology[i].ID == phenologyGroup)
                {
                    return mPhenology[i];
                }
            }
            throw new ArgumentOutOfRangeException(nameof(phenologyGroup), String.Format("Error at SpeciesSet::phenology(): invalid group: {0}", phenologyGroup));
        }

        // setup routine that opens database connection
        public void Setup(Model model)
        {
            XmlHelper xml = new XmlHelper(model.GlobalSettings.Settings.Node("model.climate"));
            this.Name = xml.GetString("tableName");
            this.climateTableQueryFilter = xml.GetString("filter");

            mYearsToLoad = xml.ValueInt("batchYears", Constant.Default.ClimateYearsToLoadPerChunk);
            mDoRandomSampling = xml.GetBool("randomSamplingEnabled", false);
            mRandomYearList.Clear();
            mRandomListIndex = -1;
            string list = xml.GetString("randomSamplingList");
            if (mDoRandomSampling)
            {
                if (String.IsNullOrEmpty(list) == false)
                {
                    List<string> strlist = Regex.Split(list, "\\W+").ToList();
                    foreach (string s in strlist)
                    {
                        mRandomYearList.Add(Int32.Parse(s));
                    }
                    // check for validity
                    foreach (int year in mRandomYearList)
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
            mDefaultTemperatureAddition = xml.GetDouble("temperatureShift", 0.0);
            mDefaultPrecipitationMultiplier = xml.GetDouble("precipitationShift", 1.0);
            if (mDefaultTemperatureAddition != 0.0 || mDefaultPrecipitationMultiplier != 1.0)
            {
                Debug.WriteLine("Climate modification: add temperature: " + mDefaultTemperatureAddition + ". Multiply precipitation: " + mDefaultPrecipitationMultiplier);
            }

            mCurrentDataYear = 0;
            mNextYearToLoad = 0;

            // setup query
            // load first chunk...
            Load(model);
            SetupPhenology(model.GlobalSettings); // load phenology
                              // setup sun
            Sun.Setup(model.ModelSettings.Latitude);
            mCurrentDataYear--; // go to "-1" -> the first call to next year will go to year 0.
            mSampledYears.Clear();
            IsSetup = true;
        }

        // setup of phenology groups
        private void SetupPhenology(GlobalSettings globalSettings)
        {
            mPhenology.Clear();
            mPhenology.Add(new Phenology(this)); // id=0
            XmlHelper xml = new XmlHelper(globalSettings.Settings.Node("model.species.phenology"));
            int i = 0;
            do
            {
                XmlNode typeNode = xml.Node(String.Format("type[{0}]", i));
                if (typeNode == null)
                {
                    break;
                }
                i++;
                int id = Int32.Parse(typeNode.Attributes["id"].Value);
                if (id < 0)
                {
                    throw new NotSupportedException(String.Format("Error setting up phenology: id invalid\ndump: {0}", xml.Dump("")));
                }
                xml.CurrentNode = typeNode;
                Phenology item = new Phenology(id, this, xml.GetDouble(".vpdMin",0.5), // use relative access to node (".x")
                                                         xml.GetDouble(".vpdMax", 5),
                                                         xml.GetDouble(".dayLengthMin", 10),
                                                         xml.GetDouble(".dayLengthMax", 11),
                                                         xml.GetDouble(".tempMin", 2),
                                                         xml.GetDouble(".tempMax", 9) );
                mPhenology.Add(item);
            } 
            while (true);
        }

        // decode "yearday" to the actual year, month, day if provided
        public void ToDate(int yearday, out int rDay, out int rMonth, out int rYear)
        {
            ClimateDay d = mDays[DayOfYear(yearday)];
            rDay = d.DayOfMonth - 1;
            rMonth = d.Month - 1;
            rYear = d.Year;
        }
    }
}
