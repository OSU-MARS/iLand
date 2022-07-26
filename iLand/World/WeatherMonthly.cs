using iLand.Input.ProjectFile;
using iLand.Input.Weather;
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
            // if time series year indices haven't been set, position them one year before the first year in the series
            // so that they become valid on the first call to OnStartYear()
            if (this.CO2ByMonth.CurrentYearStartIndex == -1)
            {
                Debug.Assert((this.CurrentDataYear == -1) && (this.CO2ByMonth.NextYearStartIndex == -1));
                this.CO2ByMonth.CurrentYearStartIndex = -Constant.MonthsInYear;
                this.CO2ByMonth.NextYearStartIndex = 0;
            }
            if (this.TimeSeries.CurrentYearStartIndex == -1)
            {
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
            this.CO2ByMonth.CurrentYearStartIndex += Constant.MonthsInYear;
            this.CO2ByMonth.NextYearStartIndex += Constant.MonthsInYear;
            this.TimeSeries.CurrentYearStartIndex += Constant.MonthsInYear;
            this.TimeSeries.NextYearStartIndex += Constant.MonthsInYear;
            if ((this.CO2ByMonth.NextYearStartIndex >= this.CO2ByMonth.Count) || (this.TimeSeries.NextYearStartIndex >= this.TimeSeries.Count))
            {
                throw new NotSupportedException("CO₂ or weather for simulation year " + this.CurrentDataYear + " is not present in weather data file '" + model.Project.World.Weather.WeatherFile + "' for at least some weather IDs."); // can't report problematic weather ID here as it's not accessible
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

            this.MeanAnnualTemperature /= Constant.MonthsInYear;

            // calculate leaf on-off phenology for deciduous species
            for (int index = 0; index < this.TreeSpeciesPhenology.Count; ++index)
            {
                this.TreeSpeciesPhenology[index].GetLeafOnAndOffDatesForCurrentYear();
            }
        }
    }
}
