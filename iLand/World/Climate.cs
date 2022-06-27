using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tool;
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
    // http://iland-model.org/ClimateData
    public class Climate
    {
        private readonly ClimateReader climateReader;

        private int mCurrentDataYear; // current year in climate data cached in memory (relative); one less than GlobalSettings.CurrentYear
        private readonly List<ClimateDay> mDays; // storage of climate data
        private readonly bool mDoRandomSampling; // if true, the sequence of years is randomized
        private readonly List<int> mMonthDayIndices; // store indices for month / years within store
        private readonly List<Phenology> mPhenology; // phenology calculations
        private readonly List<int> mRandomYearList; // for random sampling of years
        private int mRandomListIndex; // current index of the randomYearList for random sampling
        private readonly List<int> mSampledYears; // list of sampled years to use
        private readonly int mYearsToLoad; // number of years to load from database

        public int CurrentJanuary1 { get; private set; } // index of the first day of the current year (simulation timestep)
        public int NextJanuary1 { get; private set; } // index of the first day of the next year; stop index for external iterations over days in eyar
        public float CarbonDioxidePpm { get; private set; }
        /// the mean annual temperature of the current year (degree C)
        public float MeanAnnualTemperature { get; private set; }
        public float[] PrecipitationByMonth { get; private init; }
        // access to other subsystems
        public Sun Sun { get; private init; } // solar radiation class
        // get a array with mean temperatures per month (deg C)
        public float[] TemperatureByMonth { get; private init; }
        public float TotalAnnualRadiation { get; private set; } // return radiation sum (MJ) of the whole year

        public Climate(Project projectFile, string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentOutOfRangeException(nameof(name));
            }

            this.climateReader = new(projectFile, name);
            this.mCurrentDataYear = 0;
            this.mDays = new List<ClimateDay>(Constant.DaysInLeapYear); // one year minimum capacity
            this.mDoRandomSampling = projectFile.World.Climate.RandomSamplingEnabled;
            this.mMonthDayIndices = new List<int>(13); // one year minimum capacity
            this.mPhenology = new List<Phenology>();
            this.mRandomListIndex = -1;
            this.mRandomYearList = new List<int>();
            this.mSampledYears = new List<int>();
            this.mYearsToLoad = projectFile.World.Climate.BatchYears;

            this.PrecipitationByMonth = new float[Constant.MonthsInYear];
            this.Sun = new Sun();
            this.TemperatureByMonth = new float[Constant.MonthsInYear];

            // load first chunk of years
            this.climateReader.LoadGroupOfYears(projectFile, this.mYearsToLoad, this.mDays, this.mMonthDayIndices);
            this.SetupPhenology(projectFile);
            this.Sun.Setup(Maths.ToRadians(projectFile.World.Geometry.Latitude));
            this.mCurrentDataYear = -1; // go to "-1" -> the first call to next year will go to year 0.
            this.mSampledYears.Clear();

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
                        if (year < 0 || year >= this.mYearsToLoad)
                        {
                            throw new NotSupportedException("Invalid randomSamplingList! Year numbers are 0-based and must to between 0 and batchYears-1 (check value of batchYears)!!!");
                        }
                    }
                }
            }
        }

        public ClimateDay this[int index]
        {
            get { return this.mDays[index]; }
        }

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

        // returns number of days of given month (0..11)
        public int GetDaysInMonth(int month)
        {
            return this.mMonthDayIndices[this.mCurrentDataYear * Constant.MonthsInYear + month + 1] - this.mMonthDayIndices[this.mCurrentDataYear * Constant.MonthsInYear + month];
        }

        // returns number of days of current year.
        public int GetDaysInYear()
        {
            Debug.Assert(this.NextJanuary1 > this.CurrentJanuary1);
            return this.NextJanuary1 - this.CurrentJanuary1;
        }

        // activity
        public void OnStartYear(Model model)
        {
            if (this.mDoRandomSampling == false)
            {
                // default behaviour: simply advance to next year, call load() if end reached
                if (this.mCurrentDataYear >= this.mYearsToLoad - 1) // need to load more data
                {
                    this.climateReader.LoadGroupOfYears(model.Project, this.mYearsToLoad, this.mDays, this.mMonthDayIndices);
                }
                else
                {
                    ++this.mCurrentDataYear;
                }
            }
            else
            {
                // random sampling
                if (this.mRandomYearList.Count == 0)
                {
                    // random without list
                    // make sure that the sequence of years is the same for the full landscape
                    if (this.mSampledYears.Count < model.CurrentYear)
                    {
                        while (this.mSampledYears.Count - 1 < model.CurrentYear)
                        {
                            this.mSampledYears.Add(model.RandomGenerator.GetRandomInteger(0, this.mYearsToLoad));
                        }
                    }

                    this.mCurrentDataYear = this.mSampledYears[model.CurrentYear];
                }
                else
                {
                    // random with fixed list
                    ++this.mRandomListIndex;
                    if (this.mRandomListIndex >= this.mRandomYearList.Count)
                    {
                        this.mRandomListIndex = 0;
                    }
                    this.mCurrentDataYear = this.mRandomYearList[this.mRandomListIndex];
                    if (this.mCurrentDataYear >= this.mYearsToLoad)
                    {
                        throw new NotSupportedException(String.Format("Climate: load year with random sampling: the actual year {0} is invalid. Only {1} years are loaded from the climate database.", mCurrentDataYear, mYearsToLoad));
                    }
                }
                if (model.Project.Output.Logging.LogLevel >= EventLevel.Informational)
                {
                    Trace.TraceInformation("Climate: current year (randomized): " + this.mCurrentDataYear);
                }
            }

            this.CarbonDioxidePpm = model.Project.World.Climate.CO2ConcentrationInPpm;
            if (model.Project.Output.Logging.LogLevel >= EventLevel.Informational)
            {
                Trace.TraceInformation(this.mCurrentDataYear + " CO₂ concentration: " + this.CarbonDioxidePpm + " ppm.");
            }
            int currentJanuary1dayIndex = Constant.MonthsInYear * this.mCurrentDataYear;
            int nextJanuary1dayIndex = currentJanuary1dayIndex + Constant.MonthsInYear;
            if ((currentJanuary1dayIndex > this.mMonthDayIndices.Count) || (nextJanuary1dayIndex > this.mMonthDayIndices.Count))
            {
                throw new NotSupportedException("Climate data is not available for simulation year " + this.mCurrentDataYear + ".");
            }
            this.CurrentJanuary1 = this.mMonthDayIndices[this.mCurrentDataYear * Constant.MonthsInYear];
            this.NextJanuary1 = this.mMonthDayIndices[(this.mCurrentDataYear + 1) * Constant.MonthsInYear];

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
                ClimateDay day = this.mDays[dayIndex];
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
                Phenology item = new(phenology.ID, 
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
        public void ToZeroBasedDate(int dayOfYear, out int zeroBasedDay, out int zeroBasedMonth)
        {
            int indexOfDayInCurrentYear = this.CurrentJanuary1 + dayOfYear;
            ClimateDay day = this.mDays[indexOfDayInCurrentYear];
            zeroBasedDay = day.DayOfMonth - 1;
            zeroBasedMonth = day.Month - 1;
        }
    }
}
