using iLand.Input.ProjectFile;
using iLand.Input.Weather;
using System;
using Model = iLand.Simulation.Model;

namespace iLand.World
{
    internal class WeatherMonthly(Project projectFile, WeatherTimeSeriesMonthly timeSeries) 
        : Weather<WeatherTimeSeriesMonthly>(projectFile, timeSeries) // one year minimum capacity
    {
        public override void OnStartYear(Model model)
        {
            if (this.DoRandomSampling)
            {
                throw new NotImplementedException("Monthly weather does not currently support random sampling.");
            }

            ++this.CurrentDataYear;
            this.TimeSeries.CurrentYearStartIndex += Constant.Time.MonthsInYear;
            this.TimeSeries.NextYearStartIndex += Constant.Time.MonthsInYear;
            if (this.TimeSeries.NextYearStartIndex >= this.TimeSeries.Count)
            {
                throw new NotSupportedException("Weather for simulation year " + this.CurrentDataYear + " is not present in weather data file '" + model.Project.World.Weather.WeatherFile + "' for at least some weather IDs."); // can't report problematic weather ID here as it's not accessible
            }

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

            this.MeanAnnualTemperature /= Constant.Time.MonthsInYear;

            // calculate leaf on-off phenology for deciduous species
            for (int index = 0; index < this.TreeSpeciesPhenology.Count; ++index)
            {
                this.TreeSpeciesPhenology[index].GetLeafOnAndOffDatesForCurrentYear();
            }
        }
    }
}
