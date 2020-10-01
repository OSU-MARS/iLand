using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace iLand.Core
{
    // Climate handles climate input data and performs some basic related calculations on that data.
    // http://iland.boku.ac.at/ClimateData
    public class Climate
    {
        private static readonly List<int> SampledYears; // list of sampled years to use

        private string climateTableQueryFilter;
        private bool mDoRandomSampling; ///< if true, the sequence of years is randomized
        private int mYearsToLoad; // count of years to load ahead
        private int mCurrentYear; // current year (relative)
        private int mMinYear; // lowest year in store (relative)
        private int mMaxYear;  // highest year in store (relative)
        private double mDefaultTemperatureAddition; // add this to daily temp
        private double mDefaultPrecipitationMultiplier; // multiply prec with that
        private readonly List<ClimateDay> mDays; ///< storage of climate data
        private readonly List<int> mMonthDayIndices; ///< store indices for month / years within store
        private readonly List<Phenology> mPhenology; ///< phenology calculations
        private readonly List<int> mRandomYearList; ///< for random sampling of years
        private int mRandomListIndex; ///< current index of the randomYearList for random sampling

        public int Begin { get; private set; } ///< STL-like (pointer)-iterator  to the first day of the current year
        public int End { get; private set; } ///< STL-like pointer iterator to the day *after* last day of the current year
        public bool IsSetup { get; private set; }
        /// the mean annual temperature of the current year (degree C)
        public double MeanAnnualTemperature { get; private set; }
        public string Name { get; private set; } ///< table name of this climate
        public double[] PrecipitationMonth { get; private set; }
        // access to other subsystems
        public Sun Sun { get; private set; } ///< solar radiation class
        // get a array with mean temperatures per month (deg C)
        public double[] TemperatureMonth { get; private set; }
        public double TotalRadiation { get; private set; } ///< return radiation sum (MJ) of the whole year

        static Climate()
        {
            Climate.SampledYears = new List<int>();
        }

        public Climate()
        {
            this.mDays = new List<ClimateDay>(Constant.DaysInLeapYear); // one year minimum capacity
            this.mMonthDayIndices = new List<int>(13); // one year minimum capacity
            this.mPhenology = new List<Phenology>();
            this.mRandomYearList = new List<int>();

            this.PrecipitationMonth = new double[12];
            this.Sun = new Sun();
            this.TemperatureMonth = new double[12];
        }

        public ClimateDay this[int index]
        {
            get { return this.mDays[index]; }
        }

        /// annual precipitation sum (mm)
        public double AnnualPrecipitation() { double r = 0.0; for (int i = 0; i < 12; ++i) r += PrecipitationMonth[i]; return r; }
        public double DayLengthInHours(int day) { return Sun.GetDaylength(day); } ///< length of the day in hours
        // access to climate data
        public int DayOfYear(int dayofyear) { return Begin + dayofyear; } ///< get pointer to climate structure by day of year (0-based-index)
        public int WhichDayOfYear(int dayIndex) { return dayIndex - Begin; } ///< get the 0-based index of the climate given by 'climate' within the current year

        // more calculations done after loading of climate data
        private void ClimateCalculations(int lastIndex)
        {
            ClimateDay lastDayOfYear = mDays[lastIndex];
            double tau = GlobalSettings.Instance.Model.Settings.TemperatureTau;
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
            return mMonthDayIndices[mCurrentYear * 12 + month] + day;
        }

        // returns number of days of given month (0..11)
        public double Days(int month)
        {
            return (double)mMonthDayIndices[mCurrentYear * 12 + month + 1] - mMonthDayIndices[mCurrentYear * 12 + month];
        }

        // returns number of days of current year.
        public int DaysOfYear()
        {
            if (mMonthDayIndices.Count == 0)
            {
                return -1;
            }
            return End - Begin;
        }

        // load mLoadYears years from database
        private void Load()
        {
            mMinYear = mMaxYear;
            mMaxYear = mMinYear + mYearsToLoad;

            string query = String.Format("select year,month,day,min_temp,max_temp,prec,rad,vpd from {0} {1} order by year, month, day", Name, climateTableQueryFilter);
            using SqliteCommand queryCommand = new SqliteCommand(query, GlobalSettings.Instance.DatabaseClimate);
            using SqliteDataReader climateReader = queryCommand.ExecuteReader();
            climateReader.Read(); // move to first day in climate table

            int index = 0;
            int previousMonth = -1;
            int previousYear = -1;
            mMonthDayIndices.Clear();
            for (int yearLoadIndex = 0; yearLoadIndex < mYearsToLoad; yearLoadIndex++)
            {
                // check for year-specific temperature or precipitation modifier
                double precipitationMultiplier = mDefaultPrecipitationMultiplier;
                double temperatureAddition = mDefaultTemperatureAddition;
                if (GlobalSettings.Instance.Model.TimeEvents != null)
                {
                    object val_temp = GlobalSettings.Instance.Model.TimeEvents.Value(GlobalSettings.Instance.CurrentYear + yearLoadIndex, "model.climate.temperatureShift");
                    object val_prec = GlobalSettings.Instance.Model.TimeEvents.Value(GlobalSettings.Instance.CurrentYear + yearLoadIndex, "model.climate.precipitationShift");
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

                for (int daysLoadedInYear = 0; climateReader.Read(); ++index) // mStore.begin();
                {
                    ++daysLoadedInYear;
                    if (daysLoadedInYear > Constant.DaysInLeapYear)
                    {
                        throw new NotSupportedException("Error in reading climate file: attempt to read more than " + Constant.DaysInLeapYear + " days in year.");
                    }

                    ClimateDay day;
                    if (mDays.Count <= index)
                    {
                        day = new ClimateDay();
                        mDays.Add(day);
                    }
                    else
                    {
                        day = mDays[index];
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
                        mMonthDayIndices.Add(index);
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
                        break;
                    }
                }
            }

            mMonthDayIndices.Add(index); // the absolute last day...
            mCurrentYear = 0;
            Begin = mMonthDayIndices[12 * mCurrentYear];
            End = mMonthDayIndices[12 * (mCurrentYear + 1)]; // point to 1 January of the next year
            
            int lastDay = this.IndexOf(11, 30); // 31 December in zero based indexing
            ClimateCalculations(lastDay); // perform additional calculations based on the climate data loaded from the database
        }

        // returns two pointer (arguments!!!) to the begin and one after end of the given month (month: 0..11)
        public void MonthRange(int month, out int rBegin, out int rEnd)
        {
            rBegin = mMonthDayIndices[mCurrentYear * 12 + month];
            rEnd = mMonthDayIndices[mCurrentYear * 12 + month + 1];
            //Debug.WriteLine("monthRange returning: begin:"+ (*rBegin).toString() + "end-1:" + (*rEnd-1).toString();
        }

        // activity
        public void NextYear()
        {
            if (!mDoRandomSampling)
            {
                // default behaviour: simply advance to next year, call load() if end reached
                if (mCurrentYear >= mYearsToLoad - 1) // need to load more data
                {
                    // BUGBUG: support loading of additional chunks
                    throw new NotSupportedException();
                    // Load();
                }
                else
                {
                    mCurrentYear++;
                }
            }
            else
            {
                // random sampling
                if (mRandomYearList.Count == 0)
                {
                    // random without list
                    // make sure that the sequence of years is the same for the full landscape
                    if (SampledYears.Count < GlobalSettings.Instance.CurrentYear)
                    {
                        while (SampledYears.Count - 1 < GlobalSettings.Instance.CurrentYear)
                        {
                            SampledYears.Add(RandomGenerator.Random(0, mYearsToLoad));
                        }
                    }

                    mCurrentYear = SampledYears[GlobalSettings.Instance.CurrentYear];
                }
                else
                {
                    // random with fixed list
                    mRandomListIndex++;
                    if (mRandomListIndex >= mRandomYearList.Count)
                    {
                        mRandomListIndex = 0;
                    }
                    mCurrentYear = mRandomYearList[mRandomListIndex];
                    if (mCurrentYear >= mYearsToLoad)
                    {
                        throw new NotSupportedException(String.Format("Climate: load year with random sampling: the actual year {0} is invalid. Only {1} years are loaded from the climate database.", mCurrentYear, mYearsToLoad));
                    }
                }
                if (GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("Climate: current year (randomized): " + mCurrentYear);
                }
            }

            ClimateDay.CarbonDioxidePpm = GlobalSettings.Instance.Settings.GetDouble("model.climate.co2concentration", 380.0);
            if (GlobalSettings.Instance.LogDebug())
            {
                Debug.WriteLine("CO2 concentration " + ClimateDay.CarbonDioxidePpm + " ppm.");
            }
            Begin = mMonthDayIndices[mCurrentYear * 12];
            End = mMonthDayIndices[(mCurrentYear + 1) * 12]; ; // point to the 1.1. of the next year

            // some aggregates:
            // calculate radiation sum of the year and monthly precipitation
            TotalRadiation = 0.0;
            MeanAnnualTemperature = 0.0;
            for (int i = 0; i < 12; i++)
            {
                PrecipitationMonth[i] = 0.0;
                TemperatureMonth[i] = 0.0;
            }

            for (int index = Begin; index < End; ++index)
            {
                ClimateDay d = mDays[index];
                TotalRadiation += d.Radiation;
                MeanAnnualTemperature += d.MeanDaytimeTemperature;
                PrecipitationMonth[d.Month - 1] += d.Preciptitation;
                TemperatureMonth[d.Month - 1] += d.MeanDaytimeTemperature;
            }
            for (int i = 0; i < 12; ++i)
            {
                TemperatureMonth[i] /= Days(i);
            }
            MeanAnnualTemperature /= DaysOfYear();

            // calculate phenology
            for (int i = 0; i < mPhenology.Count; ++i)
            {
                mPhenology[i].Calculate();
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
        public void Setup()
        {
            GlobalSettings g = GlobalSettings.Instance;
            XmlHelper xml = new XmlHelper(g.Settings.Node("model.climate"));
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
                Debug.WriteLine("Climate modifaction: add temperature: " + mDefaultTemperatureAddition + ". Multiply precipitation: " + mDefaultPrecipitationMultiplier);
            }

            mCurrentYear = 0;
            mMinYear = 0;
            mMaxYear = 0;

            // add a where-clause
            if (String.IsNullOrEmpty(climateTableQueryFilter) == false)
            {
                climateTableQueryFilter = "where " + climateTableQueryFilter;
                Debug.WriteLine("adding climate table where-clause: " + climateTableQueryFilter);
            }

            // setup query
            // load first chunk...
            Load();
            SetupPhenology(); // load phenology
                              // setup sun
            Sun.Setup(GlobalSettings.Instance.Model.Settings.Latitude);
            mCurrentYear--; // go to "-1" -> the first call to next year will go to year 0.
            SampledYears.Clear();
            IsSetup = true;
        }

        // setup of phenology groups
        private void SetupPhenology()
        {
            mPhenology.Clear();
            mPhenology.Add(new Phenology(this)); // id=0
            XmlHelper xml = new XmlHelper(GlobalSettings.Instance.Settings.Node("model.species.phenology"));
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
