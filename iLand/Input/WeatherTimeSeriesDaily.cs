using iLand.Tool;
using System;
using System.Globalization;

namespace iLand.Input
{
    // weather variables of a day
    // http://iland-model.org/ClimateData
    public class WeatherTimeSeriesDaily : WeatherTimeSeries
    {
        public int[] DayOfMonth { get; private set; } // day of the month (1..31)
        public float[] TemperatureDaytimeMeanMA1 { get; private set; } // temperature delayed (after Mäkelä 2008) for response calculations

        public WeatherTimeSeriesDaily(Timestep timestep, int capacity)
            : base(timestep, capacity)
        {
            this.DayOfMonth = new int[capacity];
            this.TemperatureDaytimeMeanMA1 = new float[capacity];
        }

        public override void Resize(int newSize)
        {
            base.Resize(newSize);

            this.DayOfMonth = this.DayOfMonth.Resize(newSize);
            this.TemperatureDaytimeMeanMA1 = this.TemperatureDaytimeMeanMA1.Resize(newSize);
        }

        public override void Validate(int dayIndex)
        {
            base.Validate(dayIndex);

            float meanDaytimeTemperatureMA1 = this.TemperatureDaytimeMeanMA1[dayIndex];
            if (Single.IsNaN(meanDaytimeTemperatureMA1) || (meanDaytimeTemperatureMA1 < Constant.Limit.TemperatureMin) || (meanDaytimeTemperatureMA1 > Constant.Limit.TemperatureMax))
            {
                // can't reliably check against the day's min and max since strong weather systems might put the moving average outside
                // of a day's range
                DateTime date = new(this.Year[dayIndex], this.Month[dayIndex], 1);
                throw new NotSupportedException("Moving average of daily temperature " + meanDaytimeTemperatureMA1 + " °C in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN, unexpectedly low, or unexpectedly high (time series chunk index " + dayIndex + ").");
            }
        }
    }
}
