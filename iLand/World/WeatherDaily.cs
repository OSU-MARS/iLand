using iLand.Extensions;
using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Input.Weather;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Model = iLand.Simulation.Model;

namespace iLand.World
{
    public class WeatherDaily : Weather<WeatherTimeSeriesDaily>
    {
        private readonly List<int> monthDayIndices; // store indices for month / years within store
        private readonly WeatherReaderDailySql weatherReader;

        public WeatherDaily(string weatherDatabaseFilePath, string weatherTableName, Project projectFile)
            : base(projectFile, new(Timestep.Daily, Constant.DaysInLeapYear)) // one year minimum capacity
        {
            this.monthDayIndices = new(Constant.MonthsInYear + 1); // one year minimum capacity
            this.weatherReader = new(weatherDatabaseFilePath, weatherTableName, projectFile);
            this.weatherReader.LoadGroupOfYears(this.YearsToLoad, this.TimeSeries, this.monthDayIndices);
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
                    int currentSimulationYear = model.SimulationState.CurrentCalendarYear - model.Landscape.WeatherFirstCalendarYear + 1;
                    if (this.SampledYears.Count < currentSimulationYear)
                    {
                        while (this.SampledYears.Count - 1 < currentSimulationYear)
                        {
                            this.SampledYears.Add(model.RandomGenerator.GetRandomInteger(0, this.YearsToLoad));
                        }
                    }

                    this.CurrentDataYear = this.SampledYears[currentSimulationYear];
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
            int currentJanuaryIndex = this.CurrentDataYear * Constant.MonthsInYear;
            int nextJanuaryIndex = currentJanuaryIndex + Constant.MonthsInYear;
            this.CO2ByMonth.CurrentYearStartIndex = currentJanuaryIndex;
            this.CO2ByMonth.NextYearStartIndex = nextJanuaryIndex;
            this.TimeSeries.CurrentYearStartIndex = this.monthDayIndices[currentJanuaryIndex];
            this.TimeSeries.NextYearStartIndex = this.monthDayIndices[nextJanuaryIndex];

            // some aggregates
            // calculate radiation sum of the year and monthly precipitation
            this.TotalAnnualRadiation = 0.0F;
            this.MeanAnnualTemperature = 0.0F;
            for (int monthIndex = 0; monthIndex < Constant.MonthsInYear; ++monthIndex)
            {
                this.PrecipitationByMonth[monthIndex] = 0.0F;
                this.DaytimeMeanTemperatureByMonth[monthIndex] = 0.0F;
            }

            for (int weatherDayIndex = this.TimeSeries.CurrentYearStartIndex; weatherDayIndex < this.TimeSeries.NextYearStartIndex; ++weatherDayIndex)
            {
                float daytimeMeanTemperature = this.TimeSeries.TemperatureDaytimeMean[weatherDayIndex];
                int monthIndex = this.TimeSeries.Month[weatherDayIndex] - 1;
                this.DaytimeMeanTemperatureByMonth[monthIndex] += daytimeMeanTemperature;
                this.PrecipitationByMonth[monthIndex] += this.TimeSeries.PrecipitationTotalInMM[weatherDayIndex];

                this.MeanAnnualTemperature += daytimeMeanTemperature;
                this.TotalAnnualRadiation += this.TimeSeries.SolarRadiationTotal[weatherDayIndex];
            }

            bool isLeapYear = this.TimeSeries.IsCurrentlyLeapYear();
            for (int month = 0; month < Constant.MonthsInYear; ++month)
            {
                this.DaytimeMeanTemperatureByMonth[month] /= (float)DateTimeExtensions.GetDaysInMonth(month, isLeapYear);
            }
            this.MeanAnnualTemperature /= (float)DateTimeExtensions.GetDaysInYear(isLeapYear);

            // calculate leaf on-off phenology for deciduous species
            for (int index = 0; index < this.TreeSpeciesPhenology.Count; ++index)
            {
                this.TreeSpeciesPhenology[index].GetLeafOnAndOffDatesForCurrentYear();
            }
        }
    }
}
