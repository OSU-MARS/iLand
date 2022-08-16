﻿using iLand.Extensions;
using System;
using System.Globalization;

namespace iLand.Input.Weather
{
    public abstract class WeatherTimeSeries : TimeSeries
    {
        // total precipitation (daily or monthly), mm
        public float[] PrecipitationTotalInMM { get; private set; }
        // total solar radiation (daily or month), MJ/m²
        public float[] SolarRadiationTotal { get; private set; }

        // average (daily or monthly mean daily)temperature during daylight hours, °C
        public float[] TemperatureDaytimeMean { get; private set; }
        // maximum (daily or monthly mean daily) temperature
        public float[] TemperatureMax { get; private set; }
        // minimum (daily or monthly mean daily) temperature
        public float[] TemperatureMin { get; private set; }

        // mean vapor pressure deficit (daily or monthly), kPa (1 bar = 100kPa -> 10 mbar = 1 kPa)
        public float[] VpdMeanInKPa { get; private set; }

        protected WeatherTimeSeries(Timestep timestep, int capacityInTimesteps)
            : base(timestep, capacityInTimesteps)
        {
            this.PrecipitationTotalInMM = new float[capacityInTimesteps];
            this.SolarRadiationTotal = new float[capacityInTimesteps];
            this.TemperatureDaytimeMean = new float[capacityInTimesteps];
            this.TemperatureMax = new float[capacityInTimesteps];
            this.TemperatureMin = new float[capacityInTimesteps];
            this.VpdMeanInKPa = new float[capacityInTimesteps];
        }

        public int GetTimestepsPerYear()
        {
            return this.Timestep switch
            {
                Timestep.Daily => Constant.DaysInLeapYear,
                Timestep.Monthly => Constant.MonthsInYear,
                _ => throw new NotSupportedException("Unhandled weather timestep " + this.Timestep + ".")
            };
        }

        public bool IsCurrentlyLeapYear()
        {
            return DateTime.IsLeapYear(this.Year[this.CurrentYearStartIndex]);
        }

        public override void Resize(int newSize)
        {
            base.Resize(newSize);

            this.PrecipitationTotalInMM = this.PrecipitationTotalInMM.Resize(newSize);
            this.SolarRadiationTotal = this.SolarRadiationTotal.Resize(newSize);
            this.TemperatureDaytimeMean = this.TemperatureDaytimeMean.Resize(newSize);
            this.TemperatureMax = this.TemperatureMax.Resize(newSize);
            this.TemperatureMin = this.TemperatureMin.Resize(newSize);
            this.VpdMeanInKPa = this.VpdMeanInKPa.Resize(newSize);
        }

        // sanity checks
        // TODO: differentiate between daily and monthly total precipitation and solar radiation
        public override void Validate(int index)
        {
            base.Validate(index);

            float maxTemperature = this.TemperatureMax[index];
            float daytimeMeanTemperature = this.TemperatureDaytimeMean[index];
            float minTemperature = this.TemperatureMin[index];
            if (Single.IsNaN(minTemperature) || (minTemperature < Constant.Limit.TemperatureMin) || (minTemperature > daytimeMeanTemperature))
            {
                DateTime date = new(this.Year[index], this.Month[index], 1);
                throw new NotSupportedException("Minimum temperature of " + minTemperature + " °C in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN, unexpectedly low, or greater than the mean daytime temperature of " + daytimeMeanTemperature + " °C (time series chunk index " + index + ").");
            }
            if (Single.IsNaN(daytimeMeanTemperature) || (daytimeMeanTemperature > maxTemperature))
            {
                DateTime date = new(this.Year[index], this.Month[index], 1);
                throw new NotSupportedException("Daytime mean temperature of " + daytimeMeanTemperature + " °C in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN or greater than the maximum temperature of " + maxTemperature + " °C (time series chunk index " + index + ").");
            }
            if (Single.IsNaN(maxTemperature) || (maxTemperature > Constant.Limit.TemperatureMax))
            {
                DateTime date = new(this.Year[index], this.Month[index], 1);
                throw new NotSupportedException("Maximum temperature of " + maxTemperature + " °C in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is unexpectedly high (time series chunk index " + index + ").");
            }

            float totalPrecipitationInMM = this.PrecipitationTotalInMM[index];
            if (Single.IsNaN(totalPrecipitationInMM) || (totalPrecipitationInMM < 0.0F) || (totalPrecipitationInMM > Constant.Limit.MonthlyPrecipitationInMM))
            {
                DateTime date = new(this.Year[index], this.Month[index], 1);
                throw new NotSupportedException("Total precipitation of " + totalPrecipitationInMM + " mm in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN, negative, or unexpectedly high (time series chunk index " + index + ").");
            }

            float totalSolarRadiation = this.SolarRadiationTotal[index];
            if (Single.IsNaN(totalSolarRadiation) || (totalSolarRadiation < Constant.Limit.DailyTotalSolarRadiationMinimum) || (totalSolarRadiation > Constant.Limit.MonthlyTotalSolarRadiationMaximum))
            {
                DateTime date = new(this.Year[index], this.Month[index], 1);
                throw new NotSupportedException("Total solar radiation of " + totalSolarRadiation + " MJ/m² in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN, unexpectedly low, or unexpectedly high (time series chunk index " + index + ").");
            }

            float vpdInKPa = this.VpdMeanInKPa[index];
            if (Single.IsNaN(vpdInKPa) || (vpdInKPa < 0.0F) || (vpdInKPa > Constant.Limit.VaporPressureDeficitInKPa))
            {
                DateTime date = new(this.Year[index], this.Month[index], 1);
                throw new NotSupportedException("Total precipitation of " + vpdInKPa + " kPa in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN, negative, or unexpectedly high (time series chunk index " + index + ").");
            }
        }
    }
}
