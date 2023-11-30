using iLand.Extensions;
using System;
using System.Globalization;

namespace iLand.Input.Weather
{
    // weather variables of a day
    // http://iland-model.org/ClimateData
    public class WeatherTimeSeriesDaily : WeatherTimeSeries
    {
        public byte[] DayOfMonth { get; private set; } // day of the month (1..31)
        public float[] TemperatureDaytimeMeanMA1 { get; private set; } // temperature delayed (after Mäkelä 2008) for response calculations

        public WeatherTimeSeriesDaily(Timestep timestep)
            : base(timestep)
        {
            this.DayOfMonth = [];
            this.TemperatureDaytimeMeanMA1 = [];
        }

        public override void Resize(int newSize)
        {
            base.Resize(newSize);

            this.DayOfMonth = this.DayOfMonth.Resize(newSize);
            this.TemperatureDaytimeMeanMA1 = this.TemperatureDaytimeMeanMA1.Resize(newSize);
        }

        public override void Validate(int startDayIndex, int dayCount)
        {
            base.Validate(startDayIndex, dayCount);

            int endDayIndex = startDayIndex + dayCount;
            for (int dayIndex = startDayIndex; dayIndex < endDayIndex; ++dayIndex)
            {
                float meanDaytimeTemperatureMA1 = this.TemperatureDaytimeMeanMA1[dayIndex];
                if ((meanDaytimeTemperatureMA1 < Constant.Limit.TemperatureMin) || (meanDaytimeTemperatureMA1 > Constant.Limit.TemperatureMax))
                {
                    // can't reliably check against the day's min and max since strong weather systems might put the moving average outside
                    // of a day's range
                    DateTime date = new(this.Year[dayIndex], this.Month[dayIndex], 1);
                    throw new NotSupportedException("Moving average of daily temperature " + meanDaytimeTemperatureMA1 + " °C in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN, unexpectedly low, or unexpectedly high (time series chunk index " + dayIndex + ").");
                }
            }
        }
    }
}
