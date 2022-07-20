using iLand.Tool;
using System;
using System.Globalization;

namespace iLand.Input
{
    public abstract class WeatherTimeSeries
    {
        public int Count { get; set; }
        public int Capacity { get; private set; }
        public Timestep Timestep { get; private init; }

        // index of the first day or month (depending on whether the series is daily or monthly) of the current year (simulation timestep)
        public int CurrentYearStartIndex { get; set; }
        // index of the first day or month the subsequent year; stop index for external iterations over days in year
        public int NextYearStartIndex { get; set; }

        // Gregorian calendar year, CE
        public int[] Year { get; private set; }
        // month (1..12)
        public int[] Month { get; private set; }

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

        protected WeatherTimeSeries(Timestep timestep, int capacity)
        {
            this.Capacity = capacity;
            this.Count = 0;
            this.Timestep = timestep;

            this.CurrentYearStartIndex = -1;
            this.NextYearStartIndex = -1;

            this.Month = new int[capacity];
            this.PrecipitationTotalInMM = new float[capacity];
            this.SolarRadiationTotal = new float[capacity];
            this.TemperatureDaytimeMean = new float[capacity];
            this.TemperatureMax = new float[capacity];
            this.TemperatureMin = new float[capacity];
            this.VpdMeanInKPa = new float[capacity];
            this.Year = new int[capacity]; // can be shortened to one element per year if needed
        }

        public bool IsCurrentlyLeapYear()
        {
            return DateTime.IsLeapYear(this.Year[this.CurrentYearStartIndex]);
        }

        public virtual void Resize(int newSize)
        {
            this.Capacity = newSize;

            this.Month = this.Month.Resize(newSize);
            this.PrecipitationTotalInMM = this.PrecipitationTotalInMM.Resize(newSize);
            this.SolarRadiationTotal = this.SolarRadiationTotal.Resize(newSize);
            this.TemperatureDaytimeMean = this.TemperatureDaytimeMean.Resize(newSize);
            this.TemperatureMax = this.TemperatureMax.Resize(newSize);
            this.TemperatureMin = this.TemperatureMin.Resize(newSize);
            this.VpdMeanInKPa = this.VpdMeanInKPa.Resize(newSize);
            this.Year = this.Year.Resize(newSize);
        }

        // sanity checks
        public virtual void Validate(int index)
        {
            int year = this.Year[index];
            if ((year < Constant.Limit.YearMin) || (year > Constant.Limit.YearMax))
            {
                // not necessary but avoids failures if a DateTime needs to be constructed for this point in the time series
                throw new NotSupportedException("Year " + year + " is unexpectedly far in the past or the future (at time series chunk index " + index + ").");
            }

            int month = this.Month[index];
            if ((month < 1) || (month > Constant.MonthsInYear))
            {
                throw new NotSupportedException(month + " is not a valid month number in year " + year + " (at time series chunk index " + index + ").");
            }

            if (index > 0)
            {
                // basic checks for sequential date ordering
                int previousIndex = index - 1;
                int previousYear = this.Year[previousIndex];
                int yearChange = year - previousYear;
                if (yearChange < 0)
                {
                    throw new NotSupportedException("Calendar year decreases from " + previousYear + " to " + year + " instead of monotonically increasing (at time series chunk index " + index + ").");
                }
                if (yearChange > 1)
                {
                    throw new NotSupportedException("Calendar years between " + previousYear + " and " + year + " are missing from weather data (at time series chunk index " + index + ").");
                }

                int previousMonth = this.Month[previousIndex];
                int monthChange = month - previousMonth;
                if (monthChange < 0)
                {
                    if (yearChange == 0)
                    {
                        throw new NotSupportedException("Month decreases from " + previousMonth + " to " + month + " in year " + year + " instead of monotonically increasing within the calendar year (at time series chunk index " + index + ").");
                    }
                    else if (monthChange != -11)
                    {
                        throw new NotSupportedException("Month skips from " + previousMonth + " to " + month + " at transition between years " + previousYear + " and " + year + " instead of moving from December to January (at time series chunk index " + index + ").");
                    }
                }
                // not currently checked: in a daily time series monthChange == 0 implies a day of month increment
                // not currently checked: in a monthly time series monthChange should always be 1 or -11
                else if (monthChange > 1)
                {
                    throw new NotSupportedException("Month skips from " + previousMonth + " to " + month + " in year " + year + " (at time series chunk index " + index + ").");
                }
            }

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
