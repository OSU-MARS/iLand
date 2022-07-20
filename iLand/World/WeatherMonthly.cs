using iLand.Input;
using iLand.Input.ProjectFile;
using System;
using System.Diagnostics;
using Model = iLand.Simulation.Model;

namespace iLand.World
{
    internal class WeatherMonthly : Weather<WeatherTimeSeriesMonthly>
    {
        public WeatherMonthly(Project projectFile, WeatherTimeSeriesMonthly timeSeries)
            : base(projectFile, timeSeries) // one year minimum capacity
        {
            if (this.TimeSeries.CurrentYearStartIndex == -1)
            {
                // if time series year indices haven't been set, position them one year before the first year in the series
                // so that they become valid on the first call to OnStartYear()
                Debug.Assert((this.CurrentDataYear == -1) && (this.TimeSeries.NextYearStartIndex == -1));
                this.TimeSeries.CurrentYearStartIndex = -Constant.MonthsInYear;
                this.TimeSeries.NextYearStartIndex = 0;
            }
        }

        public override void OnStartYear(Model model)
        {
            if (this.DoRandomSampling)
            {
                throw new NotImplementedException("Monthly weather does not currently support random sampling.");
            }

            ++this.CurrentDataYear;
            this.TimeSeries.CurrentYearStartIndex += Constant.MonthsInYear;
            this.TimeSeries.NextYearStartIndex += Constant.MonthsInYear;
            if (this.TimeSeries.NextYearStartIndex >= this.TimeSeries.Count)
            {
                throw new NotSupportedException("Weather for simulation year " + this.CurrentDataYear + " is not present in weather data file '" + model.Project.World.Weather.File + "' for at least some weather IDs."); // can't report problematic weather ID here as it's not accessible
            }

            this.AtmosphericCO2ConcentrationInPpm = model.Project.World.Weather.CO2ConcentrationInPpm;

            // some aggregates
            // calculate radiation sum of the year and monthly precipitation
            this.TotalAnnualRadiation = 0.0F;
            this.MeanAnnualTemperature = 0.0F;
            for (int weatherMonthIndex = this.TimeSeries.CurrentYearStartIndex; weatherMonthIndex < this.TimeSeries.NextYearStartIndex; ++weatherMonthIndex)
            {
                float daytimeMeanTemperature = this.TimeSeries.TemperatureDaytimeMean[weatherMonthIndex];
                int monthIndex = this.TimeSeries.Month[weatherMonthIndex] - 1;
                this.DaytimeMeanTemperatureByMonth[monthIndex] = daytimeMeanTemperature;
                this.PrecipitationByMonth[monthIndex] = this.TimeSeries.PrecipitationTotalInMM[weatherMonthIndex];

                this.MeanAnnualTemperature += daytimeMeanTemperature;
                this.TotalAnnualRadiation += this.TimeSeries.SolarRadiationTotal[weatherMonthIndex];
            }

            this.MeanAnnualTemperature /= Constant.MonthsInYear;

            // calculate leaf on-off phenology for deciduous species
            for (int index = 0; index < this.TreeSpeciesPhenology.Count; ++index)
            {
                this.TreeSpeciesPhenology[index].GetLeafOnAndOffDatesForCurrentYear();
            }
        }
    }
}
