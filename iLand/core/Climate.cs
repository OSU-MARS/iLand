using iLand.tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace iLand.core
{
    // Climate handles climate input data and performs some basic related calculations on that data.
    // http://iland.boku.ac.at/ClimateData
    internal class Climate
    {
        private static readonly List<int> sampled_years; // list of sampled years to use

        private bool mDoRandomSampling; ///< if true, the sequence of years is randomized
        private bool mTMaxAvailable; ///< tmax is part of the climate data
        private int mLoadYears; // count of years to load ahead
        private int mCurrentYear; // current year (relative)
        private int mMinYear; // lowest year in store (relative)
        private int mMaxYear;  // highest year in store (relative)
        private double mTemperatureShift; // add this to daily temp
        private double mPrecipitationShift; // multiply prec with that
        private readonly List<ClimateDay> mStore; ///< storage of climate data
        private readonly List<int> mDayIndices; ///< store indices for month / years within store
        private SqliteDataReader mClimateQuery; ///< sql query for db access
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
            Climate.sampled_years = new List<int>();
        }

        public Climate()
        {
            this.mDayIndices = new List<int>();
            this.mPhenology = new List<Phenology>();
            this.PrecipitationMonth = new double[12];
            this.mRandomYearList = new List<int>();
            this.mStore = new List<ClimateDay>();
            this.Sun = new Sun();
            this.TemperatureMonth = new double[12];
        }

        public ClimateDay this[int index]
        {
            get { return this.mStore[index]; }
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
            ClimateDay lastDay = mStore[lastIndex];
            double tau = GlobalSettings.Instance.Model.Settings.TemperatureTau;
            // handle first day: use tissue temperature of the last day of the last year (if available)
            if (lastDay.IsValid())
            {
                mStore[0].TempDelayed = lastDay.TempDelayed + 1.0 / tau * (mStore[0].MeanDaytimeTemperature - lastDay.TempDelayed);
            }
            else
            {
                mStore[0].TempDelayed = mStore[0].MeanDaytimeTemperature;
            }

            for (int c = 1; c < mStore.Count; ++c)
            {
                // first order dynamic delayed model (Maekela 2008)
                mStore[c].TempDelayed = mStore[c - 1].TempDelayed + 1.0 / tau * (mStore[c].MeanDaytimeTemperature - mStore[c - 1].TempDelayed);
            }
        }

        // gets mStore index of climate structure of given day (0-based indices, i.e. month=11=december!)
        public int IndexOf(int month, int day)
        {
            if (mDayIndices.Count == 0)
            {
                return -1;
            }
            return mDayIndices[mCurrentYear * 12 + month] + day;
        }

        // returns number of days of given month (0..11)
        public double Days(int month)
        {
            return (double)mDayIndices[mCurrentYear * 12 + month + 1] - mDayIndices[mCurrentYear * 12 + month];
        }

        // returns number of days of current year.
        public int DaysOfYear()
        {
            if (mDayIndices.Count == 0)
            {
                return -1;
            }
            return End - Begin;
        }

        // load mLoadYears years from database
        private void Load()
        {
            if (mClimateQuery == null)
            {
                throw new NotSupportedException(String.Format("Error loading climate file - query not active."));
            }
            mMinYear = mMaxYear;
            
            mDayIndices.Clear();
            
            ClimateDay cday;
            int index = 0;
            int lastyear = -1;
            int lastDay = IndexOf(11, 30); // 31.december
            int lastmon = -1;
            for (int i = 0; i < mLoadYears; i++)
            {
                int yeardays = 0;
                if (GlobalSettings.Instance.Model.TimeEvents != null)
                {
                    object val_temp = GlobalSettings.Instance.Model.TimeEvents.Value(GlobalSettings.Instance.CurrentYear + i, "model.climate.temperatureShift");
                    object val_prec = GlobalSettings.Instance.Model.TimeEvents.Value(GlobalSettings.Instance.CurrentYear + i, "model.climate.precipitationShift");
                    if (val_temp != null)
                    {
                        mTemperatureShift = (double)val_temp;
                    }
                    if (val_prec != null)
                    {
                        mPrecipitationShift = (double)val_prec;
                    }

                    if (mTemperatureShift != 0.0 || mPrecipitationShift != 1.0)
                    {
                        Debug.WriteLine("Climate modification: add temperature:" + mTemperatureShift + ". Multiply precipitation: " + mPrecipitationShift);
                        if (mDoRandomSampling)
                        {
                            Trace.TraceWarning("WARNING - Climate: using a randomSamplingList and temperatureShift/precipitationShift at the same time. The same offset is applied for *every instance* of a year!!");
                            //throw new NotSupportedException("Climate: cannot use a randomSamplingList and temperatureShift/precipitationShift at the same time. Sorry.");
                        }
                    }
                }

                //Debug.WriteLine("loading year" + lastyear+1;
                for (; true; ++index) // mStore.begin();
                {
                    if (index >= mStore.Count)
                    {
                        throw new NotSupportedException("Error in reading climate file: read across the end!");
                    }

                    if (mClimateQuery.HasRows)
                    {
                        // rewind to start
                        Debug.WriteLine("restart of climate table");
                        throw new NotSupportedException();
                    }
                    yeardays++;
                    if (yeardays > 366)
                    {
                        throw new NotSupportedException("Error in reading climate file: yeardays>366!");
                    }

                    cday = mStore[index]; // store values directly in the List
                    cday.Year = mClimateQuery.GetInt32(0);
                    cday.Month = mClimateQuery.GetInt32(1);
                    cday.DayOfMonth = mClimateQuery.GetInt32(2);
                    if (mTMaxAvailable)
                    {
                        //References for calculation the temperature of the day:
                        //Floyd, R. B., Braddock, R. D. 1984. A simple method for fitting average diurnal temperature curves.  Agricultural and Forest Meteorology 32: 107-119.
                        //Landsberg, J. J. 1986. Physiological ecology of forest production. Academic Press Inc., 197 S.

                        cday.MinTemperature = mClimateQuery.GetDouble(3) + mTemperatureShift;
                        cday.MaxTemperature = mClimateQuery.GetDouble(4) + mTemperatureShift;
                        cday.MeanDaytimeTemperature = 0.212 * (cday.MaxTemperature - cday.MeanTemperature()) + cday.MeanTemperature();
                    }
                    else
                    {
                        // for compatibility: the old method
                        cday.MeanDaytimeTemperature = mClimateQuery.GetDouble(3) + mTemperatureShift;
                        cday.MinTemperature = mClimateQuery.GetDouble(4) + mTemperatureShift;
                        cday.MaxTemperature = cday.MeanDaytimeTemperature;
                    }
                    cday.Preciptitation = mClimateQuery.GetDouble(5) * mPrecipitationShift;
                    cday.Radiation = mClimateQuery.GetDouble(6);
                    cday.Vpd = mClimateQuery.GetDouble(7);
                    // sanity checks
                    if (cday.Month < 1 || cday.DayOfMonth < 1 || cday.Month > 12 || cday.DayOfMonth > 31)
                    {
                        Debug.WriteLine(String.Format("Invalid dates in climate table {0}: year {1} month {2} day {3}!", Name, cday.Year, cday.Month, cday.DayOfMonth));
                    }
                    Debug.WriteLineIf(cday.Month < 1 || cday.DayOfMonth < 1 || cday.Month > 12 || cday.DayOfMonth > 31, "Climate:load", "invalid dates");
                    Debug.WriteLineIf(cday.MeanDaytimeTemperature < -70 || cday.MeanDaytimeTemperature > 50, "Climate:load", "temperature out of range (-70..+50 degree C)");
                    Debug.WriteLineIf(cday.Preciptitation < 0 || cday.Preciptitation > 200, "Climate:load", "precipitation out of range (0..200mm)");
                    Debug.WriteLineIf(cday.Radiation < 0 || cday.Radiation > 50, "Climate:load", "radiation out of range (0..50 MJ/m2/day)");
                    Debug.WriteLineIf(cday.Vpd < 0 || cday.Vpd > 10, "Climate:load", "vpd out of range (0..10 kPa)");

                    if (cday.Month != lastmon)
                    {
                        // new month...
                        lastmon = cday.Month;
                        // save relative position of the beginning of the new month
                        mDayIndices.Add(index);
                    }
                    if (yeardays == 1)
                    {
                        // check on first day of the year
                        if (lastyear != -1 && cday.Year != lastyear + 1)
                        {
                            throw new NotSupportedException(String.Format("Error in reading climate file: invalid year break at y-m-d: {0}-{1}-{2}!", cday.Year, cday.Month, cday.DayOfMonth));
                        }
                    }

                    mClimateQuery.Read();
                    if (cday.Month == 12 && cday.DayOfMonth == 31)
                    {
                        break;
                    }
                }
                lastyear = cday.Year;
            }
            for (;  index < mStore.Count; index++)
            {
                // BUGBUG: use of loop here is inconsistent with calls to mDayIndices.Add()
                mStore[index] = null; // save a invalid day at the end...
            }

            mDayIndices.Add(index); // the absolute last day...
            mMaxYear = mMinYear + mLoadYears;
            mCurrentYear = 0;
            Begin = mDayIndices[mCurrentYear * 12];
            End = mDayIndices[(mCurrentYear + 1) * 12]; ; // point to the 1.1. of the next year

            ClimateCalculations(lastDay); // perform additional calculations based on the climate data loaded from the database
        }

        // returns two pointer (arguments!!!) to the begin and one after end of the given month (month: 0..11)
        public void MonthRange(int month, out int rBegin, out int rEnd)
        {
            rBegin = mDayIndices[mCurrentYear * 12 + month];
            rEnd = mDayIndices[mCurrentYear * 12 + month + 1];
            //Debug.WriteLine("monthRange returning: begin:"+ (*rBegin).toString() + "end-1:" + (*rEnd-1).toString();
        }

        // activity
        public void NextYear()
        {
            if (!mDoRandomSampling)
            {
                // default behaviour: simply advance to next year, call load() if end reached
                if (mCurrentYear >= mLoadYears - 1) // need to load more data
                {
                    Load();
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
                    if (sampled_years.Count < GlobalSettings.Instance.CurrentYear)
                    {
                        while (sampled_years.Count - 1 < GlobalSettings.Instance.CurrentYear)
                        {
                            sampled_years.Add(RandomGenerator.Random(0, mLoadYears));
                        }
                    }

                    mCurrentYear = sampled_years[GlobalSettings.Instance.CurrentYear];
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
                    if (mCurrentYear >= mLoadYears)
                    {
                        throw new NotSupportedException(String.Format("Climate: load year with random sampling: the actual year {0} is invalid. Only {1} years are loaded from the climate database.", mCurrentYear, mLoadYears));
                    }
                }
                if (GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("Climate: current year (randomized): " + mCurrentYear);
                }
            }

            ClimateDay.CarbonDioxidePpm = GlobalSettings.Instance.Settings.ValueDouble("model.climate.co2concentration", 380.0);
            if (GlobalSettings.Instance.LogDebug())
            {
                Debug.WriteLine("CO2 concentration " + ClimateDay.CarbonDioxidePpm + " ppm.");
            }
            Begin = mDayIndices[mCurrentYear * 12];
            End = mDayIndices[(mCurrentYear + 1) * 12]; ; // point to the 1.1. of the next year

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
                ClimateDay d = mStore[index];
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
            string tableName = xml.Value("tableName");
            Name = tableName;
            string filter = xml.Value("filter");

            mLoadYears = (int)Math.Max(xml.ValueDouble("batchYears", 1.0), 1.0);
            mDoRandomSampling = xml.ValueBool("randomSamplingEnabled", false);
            mRandomYearList.Clear();
            mRandomListIndex = -1;
            string list = xml.Value("randomSamplingList");
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
                        if (year < 0 || year >= mLoadYears)
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
            mTemperatureShift = xml.ValueDouble("temperatureShift", 0.0);
            mPrecipitationShift = xml.ValueDouble("precipitationShift", 1.0);
            if (mTemperatureShift != 0.0 || mPrecipitationShift != 1.0)
            {
                Debug.WriteLine("Climate modifaction: add temperature: " + mTemperatureShift + ". Multiply precipitation: " + mPrecipitationShift);
            }

            mStore.Capacity = mLoadYears * 366 + 1; // reserve enough space (1 more than used at max)
            mCurrentYear = 0;
            mMinYear = 0;
            mMaxYear = 0;

            // add a where-clause
            if (String.IsNullOrEmpty(filter) == false)
            {
                filter = String.Format("where {0}", filter);
                Debug.WriteLine("adding climate table where-clause: " + filter);
            }

            string query = String.Format("select year,month,day,min_temp,max_temp,prec,rad,vpd from {0} {1} order by year, month, day", tableName, filter);
            // here add more options...
            SqliteCommand queryCommand = new SqliteCommand(query, g.DatabaseClimate);
            mClimateQuery = queryCommand.ExecuteReader();
            mTMaxAvailable = true;

            // setup query
            // load first chunk...
            Load();
            SetupPhenology(); // load phenology
                              // setup sun
            Sun.Setup(GlobalSettings.Instance.Model.Settings.Latitude);
            mCurrentYear--; // go to "-1" -> the first call to next year will go to year 0.
            sampled_years.Clear();
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
                XmlNode n = xml.Node(String.Format("type[{0}]", i));
                if (n != null)
                {
                    break;
                }
                i++;
                int id = Int32.Parse(n.Attributes["id"].Value);
                if (id < 0)
                {
                    throw new NotSupportedException(String.Format("Error setting up phenology: id invalid\ndump: {0}", xml.Dump("")));
                }
                xml.CurrentNode = n;
                Phenology item = new Phenology(id, this, xml.ValueDouble(".vpdMin",0.5), // use relative access to node (".x")
                                                         xml.ValueDouble(".vpdMax", 5),
                                                         xml.ValueDouble(".dayLengthMin", 10),
                                                         xml.ValueDouble(".dayLengthMax", 11),
                                                         xml.ValueDouble(".tempMin", 2),
                                                         xml.ValueDouble(".tempMax", 9) );
                mPhenology.Add(item);
            } 
            while (true);
        }

        // decode "yearday" to the actual year, month, day if provided
        public void ToDate(int yearday, out int rDay, out int rMonth, out int rYear)
        {
            ClimateDay d = mStore[DayOfYear(yearday)];
            rDay = d.DayOfMonth - 1;
            rMonth = d.Month - 1;
            rYear = d.Year;
        }
    }
}
