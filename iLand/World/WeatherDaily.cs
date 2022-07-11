using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tool;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Model = iLand.Simulation.Model;

namespace iLand.World
{
    public class WeatherDaily : Weather
    {
        private readonly List<int> monthDayIndices; // store indices for month / years within store
        private readonly WeatherReaderDailySql weatherReader;

        public int CurrentJanuary1 { get; private set; } // index of the first day of the current year (simulation timestep)
        public int NextJanuary1 { get; private set; } // index of the first day of the next year; stop index for external iterations over days in eyar
        public WeatherTimeSeriesDaily TimeSeries { get; private init; } // storage of weather data

        public WeatherDaily(Project projectFile, string weatherTableName)
            : base(projectFile)
        {
            if (String.IsNullOrEmpty(weatherTableName))
            {
                throw new ArgumentOutOfRangeException(nameof(weatherTableName));
            }

            this.TimeSeries = new(Timestep.Daily, Constant.DaysInLeapYear); // one year minimum capacity
            this.monthDayIndices = new(Constant.MonthsInYear + 1); // one year minimum capacity
            this.weatherReader = new(projectFile, weatherTableName);
            this.weatherReader.LoadGroupOfYears(this.YearsToLoad, this.TimeSeries, this.monthDayIndices);
        }

        // returns number of days of given month (0..11)
        public int GetDaysInMonth(int month)
        {
            return this.monthDayIndices[this.CurrentDataYear * Constant.MonthsInYear + month + 1] - this.monthDayIndices[this.CurrentDataYear * Constant.MonthsInYear + month];
        }

        // returns number of days of current year.
        public int GetDaysInYear()
        {
            Debug.Assert(this.NextJanuary1 > this.CurrentJanuary1);
            return this.NextJanuary1 - this.CurrentJanuary1;
        }

        public override void OnStartYear(Model model)
        {
            if (this.DoRandomSampling == false)
            {
                // default behaviour: simply advance to next year, call load() if end reached
                if (this.CurrentDataYear >= this.YearsToLoad - 1) // need to load more data
                {
                    this.weatherReader.LoadGroupOfYears(this.YearsToLoad, this.TimeSeries, this.monthDayIndices);
                }
                else
                {
                    ++this.CurrentDataYear;
                }
            }
            else
            {
                // random sampling
                if (this.RandomYearList.Count == 0)
                {
                    // random without list
                    // make sure that the sequence of years is the same for the full landscape
                    if (this.SampledYears.Count < model.CurrentYear)
                    {
                        while (this.SampledYears.Count - 1 < model.CurrentYear)
                        {
                            this.SampledYears.Add(model.RandomGenerator.GetRandomInteger(0, this.YearsToLoad));
                        }
                    }

                    this.CurrentDataYear = this.SampledYears[model.CurrentYear];
                }
                else
                {
                    // random with fixed list
                    ++this.RandomListIndex;
                    if (this.RandomListIndex >= this.RandomYearList.Count)
                    {
                        this.RandomListIndex = 0;
                    }
                    this.CurrentDataYear = this.RandomYearList[this.RandomListIndex];
                    if (this.CurrentDataYear >= this.YearsToLoad)
                    {
                        throw new NotSupportedException(String.Format("Load year with random sampling: the actual year {0} is invalid. Only {1} years are loaded from the weather database.", CurrentDataYear, YearsToLoad));
                    }
                }
                if (model.Project.Output.Logging.LogLevel >= EventLevel.Informational)
                {
                    Trace.TraceInformation("Current year (randomized): " + this.CurrentDataYear);
                }
            }

            this.AtmosphericCO2ConcentrationInPpm = model.Project.World.Weather.CO2ConcentrationInPpm;
            //if (model.Project.Output.Logging.LogLevel >= EventLevel.Informational)
            //{
            //    Trace.TraceInformation(this.currentDataYear + " CO₂ concentration: " + this.CarbonDioxidePpm + " ppm.");
            //}
            int currentJanuary1dayIndex = Constant.MonthsInYear * this.CurrentDataYear;
            int nextJanuary1dayIndex = currentJanuary1dayIndex + Constant.MonthsInYear;
            if ((currentJanuary1dayIndex > this.monthDayIndices.Count) || (nextJanuary1dayIndex > this.monthDayIndices.Count))
            {
                throw new NotSupportedException("Weather data is not available for simulation year " + this.CurrentDataYear + ".");
            }
            this.CurrentJanuary1 = this.monthDayIndices[this.CurrentDataYear * Constant.MonthsInYear];
            this.NextJanuary1 = this.monthDayIndices[(this.CurrentDataYear + 1) * Constant.MonthsInYear];

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
                int monthIndex = this.TimeSeries.Month[dayIndex] - 1;
                this.PrecipitationByMonth[monthIndex] += this.TimeSeries.PrecipitationTotalInMM[dayIndex];
                float daytimeMeanTemperature = this.TimeSeries.TemperatureDaytimeMean[dayIndex];
                this.TemperatureByMonth[monthIndex] += daytimeMeanTemperature;

                this.MeanAnnualTemperature += daytimeMeanTemperature;
                this.TotalAnnualRadiation += this.TimeSeries.SolarRadiationTotal[dayIndex];
            }
            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                this.TemperatureByMonth[month] /= this.GetDaysInMonth(month);
            }
            this.MeanAnnualTemperature /= this.GetDaysInYear();

            // calculate phenology
            for (int index = 0; index < this.TreeSpeciesPhenology.Count; ++index)
            {
                this.TreeSpeciesPhenology[index].RunYear();
            }
        }

        // decode "yearday" to the actual year, month, day if provided
        public void ToZeroBasedDate(int dayOfYear, out int zeroBasedDay, out int zeroBasedMonth)
        {
            int dayIndex = this.CurrentJanuary1 + dayOfYear;
            zeroBasedDay = this.TimeSeries.DayOfMonth[dayIndex] - 1;
            zeroBasedMonth = this.TimeSeries.Month[dayIndex] - 1;
        }
    }
}
