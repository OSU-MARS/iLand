using iLand.Tool;
using System;
using System.Globalization;

namespace iLand.Input
{
    public class WeatherTimeSeriesMonthly : WeatherTimeSeries
    {
        public float[] SnowTotalInMM { get; private set; }

        public WeatherTimeSeriesMonthly(Timestep timestep, int capacity)
            : base(timestep, capacity)
        {
            this.SnowTotalInMM = new float[capacity];
        }

        public override void Resize(int newSize)
        {
            base.Resize(newSize);

            this.SnowTotalInMM = this.SnowTotalInMM.Resize(newSize);
        }

        public override void Validate(int monthIndex)
        {
            base.Validate(monthIndex);

            float totalPrecipitationInMM = this.PrecipitationTotalInMM[monthIndex];
            float totalSnowInMM = this.SnowTotalInMM[monthIndex];
            if (Single.IsNaN(totalSnowInMM) || (totalSnowInMM < 0.0F) || (totalSnowInMM > totalPrecipitationInMM))
            {
                DateTime date = new(this.Year[monthIndex], this.Month[monthIndex], 1);
                throw new NotSupportedException("Total monthly snowfall of " + totalSnowInMM + " mm in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture)  + " is NaN, negative, or exceeds the total monthly precipitation of " + totalPrecipitationInMM + " mm (time series chunk index " + monthIndex + ").");
            }
        }
    }
}
