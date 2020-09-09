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
        private static List<int> sampled_years; // list of sampled years to use

        private bool mIsSetup;
        private bool mDoRandomSampling; ///< if true, the sequence of years is randomized
        private bool mTMaxAvailable; ///< tmax is part of the climate data
        private string mName;
        private Sun mSun; ///< class doing solar radiation calculations
        private int mLoadYears; // count of years to load ahead
        private int mCurrentYear; // current year (relative)
        private int mMinYear; // lowest year in store (relative)
        private int mMaxYear;  // highest year in store (relative)
        private double mTemperatureShift; // add this to daily temp
        private double mPrecipitationShift; // multiply prec with that
        private int mBegin; // index of the first day of the current year
        private int mEnd; // index of the last day of the current year (+1)
        private List<ClimateDay> mStore; ///< storage of climate data
        private List<int> mDayIndices; ///< store indices for month / years within store
        private SqliteDataReader mClimateQuery; ///< sql query for db access
        private List<Phenology> mPhenology; ///< phenology calculations
        private List<int> mRandomYearList; ///< for random sampling of years
        private int mRandomListIndex; ///< current index of the randomYearList for random sampling
        private double mAnnualRadiation;  ///< this year's value for total radiation (MJ/m2)
        private double[] mPrecipitationMonth; ///< this years preciptitation sum (mm) per month
        private double[] mTemperatureMonth; ///< this years average temperature per month
        private double mMeanAnnualTemperature; ///< mean temperature of the current year

        public Climate()
        {
            this.mDayIndices = new List<int>();
            this.mPhenology = new List<Phenology>();
            this.mPrecipitationMonth = new double[12];
            this.mRandomYearList = new List<int>();
            this.mStore = new List<ClimateDay>();
            this.mTemperatureMonth = new double[12];
        }

        public ClimateDay this[int index]
        {
            get { return this.mStore[index]; }
        }

        /// annual precipitation sum (mm)
        public double annualPrecipitation() { double r = 0.0; for (int i = 0; i < 12; ++i) r += mPrecipitationMonth[i]; return r; }
        public int begin() { return mBegin; } ///< STL-like (pointer)-iterator  to the first day of the current year
        public double daylength_h(int day) { return sun().daylength(day); } ///< length of the day in hours
        // access to climate data
        public int dayOfYear(int dayofyear) { return mBegin + dayofyear; } ///< get pointer to climate structure by day of year (0-based-index)
        public int end() { return mEnd; } ///< STL-like pointer iterator to the day *after* last day of the current year
        public bool isSetup() { return mIsSetup; }
        /// the mean annual temperature of the current year (degree C)
        public double meanAnnualTemperature() { return mMeanAnnualTemperature; }
        public string name() { return mName; } ///< table name of this climate
        public double[] precipitationMonth() { return mPrecipitationMonth; }
        // access to other subsystems
        public Sun sun() { return mSun; } ///< solar radiation class
        // get a array with mean temperatures per month (deg C)
        public double[] temperatureMonth() { return mTemperatureMonth; }
        public double totalRadiation() { return mAnnualRadiation; } ///< return radiation sum (MJ) of the whole year
        public int whichDayOfYear(int dayIndex) { return dayIndex - mBegin; } ///< get the 0-based index of the climate given by 'climate' within the current year

        // more calculations done after loading of climate data
        private void climateCalculations(int lastIndex)
        {
            ClimateDay lastDay = mStore[lastIndex];
            double tau = GlobalSettings.instance().model().settings().temperatureTau;
            // handle first day: use tissue temperature of the last day of the last year (if available)
            if (lastDay.isValid())
            {
                mStore[0].temp_delayed = lastDay.temp_delayed + 1.0 / tau * (mStore[0].temperature - lastDay.temp_delayed);
            }
            else
            {
                mStore[0].temp_delayed = mStore[0].temperature;
            }

            for (int c = 1; c < mStore.Count; ++c)
            {
                // first order dynamic delayed model (Maekela 2008)
                mStore[c].temp_delayed = mStore[c - 1].temp_delayed + 1.0 / tau * (mStore[c].temperature - mStore[c - 1].temp_delayed);
            }
        }

        // gets mStore index of climate structure of given day (0-based indices, i.e. month=11=december!)
        public int day(int month, int day)
        {
            if (mDayIndices.Count == 0)
            {
                return -1;
            }
            return mDayIndices[mCurrentYear * 12 + month] + day;
        }

        // returns number of days of given month (0..11)
        public double days(int month)
        {
            return (double)mDayIndices[mCurrentYear * 12 + month + 1] - mDayIndices[mCurrentYear * 12 + month];
        }

        // returns number of days of current year.
        public int daysOfYear()
        {
            if (mDayIndices.Count == 0)
            {
                return -1;
            }
            return mEnd - mBegin;
        }

        // load mLoadYears years from database
        private void load()
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
            int lastDay = day(11, 30); // 31.december
            int lastmon = -1;
            for (int i = 0; i < mLoadYears; i++)
            {
                int yeardays = 0;
                if (GlobalSettings.instance().model().timeEvents() != null)
                {
                    object val_temp = GlobalSettings.instance().model().timeEvents().value(GlobalSettings.instance().currentYear() + i, "model.climate.temperatureShift");
                    object val_prec = GlobalSettings.instance().model().timeEvents().value(GlobalSettings.instance().currentYear() + i, "model.climate.precipitationShift");
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
                        lastyear = -1;
                        throw new NotSupportedException();
                    }
                    yeardays++;
                    if (yeardays > 366)
                    {
                        throw new NotSupportedException("Error in reading climate file: yeardays>366!");
                    }

                    cday = mStore[index]; // store values directly in the List
                    cday.year = mClimateQuery.GetInt32(0);
                    cday.month = mClimateQuery.GetInt32(1);
                    cday.dayOfMonth = mClimateQuery.GetInt32(2);
                    if (mTMaxAvailable)
                    {
                        //References for calculation the temperature of the day:
                        //Floyd, R. B., Braddock, R. D. 1984. A simple method for fitting average diurnal temperature curves.  Agricultural and Forest Meteorology 32: 107-119.
                        //Landsberg, J. J. 1986. Physiological ecology of forest production. Academic Press Inc., 197 S.

                        cday.min_temperature = mClimateQuery.GetDouble(3) + mTemperatureShift;
                        cday.max_temperature = mClimateQuery.GetDouble(4) + mTemperatureShift;
                        cday.temperature = 0.212 * (cday.max_temperature - cday.mean_temp()) + cday.mean_temp();
                    }
                    else
                    {
                        // for compatibility: the old method
                        cday.temperature = mClimateQuery.GetDouble(3) + mTemperatureShift;
                        cday.min_temperature = mClimateQuery.GetDouble(4) + mTemperatureShift;
                        cday.max_temperature = cday.temperature;
                    }
                    cday.preciptitation = mClimateQuery.GetDouble(5) * mPrecipitationShift;
                    cday.radiation = mClimateQuery.GetDouble(6);
                    cday.vpd = mClimateQuery.GetDouble(7);
                    // sanity checks
                    if (cday.month < 1 || cday.dayOfMonth < 1 || cday.month > 12 || cday.dayOfMonth > 31)
                    {
                        Debug.WriteLine(String.Format("Invalid dates in climate table {0}: year {1} month {2} day {3}!", name(), cday.year, cday.month, cday.dayOfMonth));
                    }
                    Debug.WriteLineIf(cday.month < 1 || cday.dayOfMonth < 1 || cday.month > 12 || cday.dayOfMonth > 31, "Climate:load", "invalid dates");
                    Debug.WriteLineIf(cday.temperature < -70 || cday.temperature > 50, "Climate:load", "temperature out of range (-70..+50 degree C)");
                    Debug.WriteLineIf(cday.preciptitation < 0 || cday.preciptitation > 200, "Climate:load", "precipitation out of range (0..200mm)");
                    Debug.WriteLineIf(cday.radiation < 0 || cday.radiation > 50, "Climate:load", "radiation out of range (0..50 MJ/m2/day)");
                    Debug.WriteLineIf(cday.vpd < 0 || cday.vpd > 10, "Climate:load", "vpd out of range (0..10 kPa)");

                    if (cday.month != lastmon)
                    {
                        // new month...
                        lastmon = cday.month;
                        // save relative position of the beginning of the new month
                        mDayIndices.Add(index);
                    }
                    if (yeardays == 1)
                    {
                        // check on first day of the year
                        if (lastyear != -1 && cday.year != lastyear + 1)
                        {
                            throw new NotSupportedException(String.Format("Error in reading climate file: invalid year break at y-m-d: {0}-{1}-{2}!", cday.year, cday.month, cday.dayOfMonth));
                        }
                    }

                    mClimateQuery.Read();
                    if (cday.month == 12 && cday.dayOfMonth == 31)
                    {
                        break;
                    }
                }
                lastyear = cday.year;
            }
            for (;  index < mStore.Count; index++)
            {
                // BUGBUG: use of loop here is inconsistent with calls to mDayIndices.Add()
                mStore[index] = null; // save a invalid day at the end...
            }

            mDayIndices.Add(index); // the absolute last day...
            mMaxYear = mMinYear + mLoadYears;
            mCurrentYear = 0;
            mBegin = mDayIndices[mCurrentYear * 12];
            mEnd = mDayIndices[(mCurrentYear + 1) * 12]; ; // point to the 1.1. of the next year

            climateCalculations(lastDay); // perform additional calculations based on the climate data loaded from the database
        }

        // returns two pointer (arguments!!!) to the begin and one after end of the given month (month: 0..11)
        public void monthRange(int month, out int rBegin, out int rEnd)
        {
            rBegin = mDayIndices[mCurrentYear * 12 + month];
            rEnd = mDayIndices[mCurrentYear * 12 + month + 1];
            //Debug.WriteLine("monthRange returning: begin:"+ (*rBegin).toString() + "end-1:" + (*rEnd-1).toString();
        }

        // activity
        public void nextYear()
        {
            if (!mDoRandomSampling)
            {
                // default behaviour: simply advance to next year, call load() if end reached
                if (mCurrentYear >= mLoadYears - 1) // need to load more data
                {
                    load();
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
                    if (sampled_years.Count < GlobalSettings.instance().currentYear())
                    {
                        while (sampled_years.Count - 1 < GlobalSettings.instance().currentYear())
                        {
                            sampled_years.Add(RandomGenerator.irandom(0, mLoadYears));
                        }
                    }

                    mCurrentYear = sampled_years[GlobalSettings.instance().currentYear()];
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
                if (GlobalSettings.instance().logLevelDebug())
                {
                    Debug.WriteLine("Climate: current year (randomized): " + mCurrentYear);
                }
            }

            ClimateDay.co2 = GlobalSettings.instance().settings().valueDouble("model.climate.co2concentration", 380.0);
            if (GlobalSettings.instance().logLevelDebug())
            {
                Debug.WriteLine("CO2 concentration " + ClimateDay.co2 + " ppm.");
            }
            mBegin = mDayIndices[mCurrentYear * 12];
            mEnd = mDayIndices[(mCurrentYear + 1) * 12]; ; // point to the 1.1. of the next year

            // some aggregates:
            // calculate radiation sum of the year and monthly precipitation
            mAnnualRadiation = 0.0;
            mMeanAnnualTemperature = 0.0;
            for (int i = 0; i < 12; i++)
            {
                mPrecipitationMonth[i] = 0.0;
                mTemperatureMonth[i] = 0.0;
            }

            for (int index = begin(); index < end(); ++index)
            {
                ClimateDay d = mStore[index];
                mAnnualRadiation += d.radiation;
                mMeanAnnualTemperature += d.temperature;
                mPrecipitationMonth[d.month - 1] += d.preciptitation;
                mTemperatureMonth[d.month - 1] += d.temperature;
            }
            for (int i = 0; i < 12; ++i)
            {
                mTemperatureMonth[i] /= days(i);
            }
            mMeanAnnualTemperature /= daysOfYear();

            // calculate phenology
            for (int i = 0; i < mPhenology.Count; ++i)
            {
                mPhenology[i].calculate();
            }
        }

        // phenology class of given type
        public Phenology phenology(int phenologyGroup)
        {
            Phenology p = mPhenology[phenologyGroup];
            if (p.id() == phenologyGroup)
            {
                return p;
            }

            // search...
            for (int i = 0; i < mPhenology.Count; i++)
            {
                if (mPhenology[i].id() == phenologyGroup)
                {
                    return mPhenology[i];
                }
            }
            throw new ArgumentOutOfRangeException(nameof(phenologyGroup), String.Format("Error at SpeciesSet::phenology(): invalid group: {0}", phenologyGroup));
        }

        // setup routine that opens database connection
        public void setup()
        {
            GlobalSettings g = GlobalSettings.instance();
            XmlHelper xml = new XmlHelper(g.settings().node("model.climate"));
            string tableName = xml.value("tableName");
            mName = tableName;
            string filter = xml.value("filter");

            mLoadYears = (int)Math.Max(xml.valueDouble("batchYears", 1.0), 1.0);
            mDoRandomSampling = xml.valueBool("randomSamplingEnabled", false);
            mRandomYearList.Clear();
            mRandomListIndex = -1;
            string list = xml.value("randomSamplingList");
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
                    Debug.WriteLine("Climate: Random sampling enabled with fixed list " + mRandomYearList.Count + " of years. climate: " + name());
                }
                else
                {
                    Debug.WriteLine("Climate: Random sampling enabled (without a fixed list). climate: " + name());
                }
            }
            mTemperatureShift = xml.valueDouble("temperatureShift", 0.0);
            mPrecipitationShift = xml.valueDouble("precipitationShift", 1.0);
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
            SqliteCommand queryCommand = new SqliteCommand(query, g.dbclimate());
            mClimateQuery = queryCommand.ExecuteReader();
            mTMaxAvailable = true;

            // setup query
            // load first chunk...
            load();
            setupPhenology(); // load phenology
                              // setup sun
            mSun.setup(GlobalSettings.instance().model().settings().latitude);
            mCurrentYear--; // go to "-1" -> the first call to next year will go to year 0.
            sampled_years.Clear();
            mIsSetup = true;
        }

        // setup of phenology groups
        private void setupPhenology()
        {
            mPhenology.Clear();
            mPhenology.Add(new Phenology(this)); // id=0
            XmlHelper xml = new XmlHelper(GlobalSettings.instance().settings().node("model.species.phenology"));
            int i = 0;
            do
            {
                XmlNode n = xml.node(String.Format("type[{0}]", i));
                if (n != null)
                {
                    break;
                }
                i++;
                int id = Int32.Parse(n.Attributes["id"].Value);
                if (id < 0)
                {
                    throw new NotSupportedException(String.Format("Error setting up phenology: id invalid\ndump: {0}", xml.dump("")));
                }
                xml.setCurrentNode(n);
                Phenology item = new Phenology(id, this, xml.valueDouble(".vpdMin",0.5), // use relative access to node (".x")
                                                         xml.valueDouble(".vpdMax", 5),
                                                         xml.valueDouble(".dayLengthMin", 10),
                                                         xml.valueDouble(".dayLengthMax", 11),
                                                         xml.valueDouble(".tempMin", 2),
                                                         xml.valueDouble(".tempMax", 9) );
                mPhenology.Add(item);
            } 
            while (true);
        }

        // decode "yearday" to the actual year, month, day if provided
        public void toDate(int yearday, out int rDay, out int rMonth, out int rYear)
        {
            ClimateDay d = mStore[dayOfYear(yearday)];
            rDay = d.dayOfMonth - 1;
            rMonth = d.month - 1;
            rYear = d.year;
        }
    }
}
