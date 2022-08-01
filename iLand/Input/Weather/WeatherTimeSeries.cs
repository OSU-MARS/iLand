﻿using iLand.Tool;
using System;
using System.Globalization;

namespace iLand.Input.Weather
{
    public abstract class WeatherTimeSeries : TimeSeries
    {
        // sum of day [mm]
        public float[] PrecipitationTotalInMM { get; private set; }
        // sum of day (MJ/m²)
        public float[] SolarRadiationTotal { get; private set; }

        // daily average degree C during daylight hours
        public float[] TemperatureDaytimeMean { get; private set; }
        // maximum temperature of the day
        public float[] TemperatureMax { get; private set; }
        // minimum temperature of the day
        public float[] TemperatureMin { get; private set; }

        // average of day [kPa] = [0.1 mbar] (1 bar = 100kPa)
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
            if (Single.IsNaN(totalSolarRadiation) || (totalSolarRadiation < 0.0F) || (totalSolarRadiation > Constant.Limit.DailySolarRadiation))
            {
                DateTime date = new(this.Year[index], this.Month[index], 1);
                throw new NotSupportedException("Total solar radiation of " + totalSolarRadiation + " MJ/m² in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN, negative, or unexpectedly high (time series chunk index " + index + ").");
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