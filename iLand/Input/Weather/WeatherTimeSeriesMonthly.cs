using iLand.Extensions;
using System;
using System.Globalization;

namespace iLand.Input.Weather
{
    public class WeatherTimeSeriesMonthly : WeatherTimeSeries
    {
        public float[] SnowTotalInMM { get; private set; }

        public WeatherTimeSeriesMonthly(Timestep timestep)
            : base(timestep)
        {
            // position time series year indices one year before the first year in the series so that they become valid on
            // the first call to OnStartYear()
            this.CurrentYearStartIndex = -Constant.Time.MonthsInYear;
            this.NextYearStartIndex = 0;

            this.SnowTotalInMM = Array.Empty<float>();
        }

        public override void Resize(int newSize)
        {
            base.Resize(newSize);

            this.SnowTotalInMM = this.SnowTotalInMM.Resize(newSize);
        }

        public override void Validate(int startMonthIndex, int monthCount)
        {
            base.Validate(startMonthIndex, monthCount);

            int endMonthIndex = startMonthIndex + monthCount;
            for (int monthIndex = startMonthIndex; monthIndex < endMonthIndex; ++monthIndex)
            {
                float totalPrecipitationInMM = this.PrecipitationTotalInMM[monthIndex];
                float totalSnowInMM = this.SnowTotalInMM[monthIndex];
                if ((totalSnowInMM < 0.0F) || (totalSnowInMM > totalPrecipitationInMM))
                {
                    DateTime date = new(this.Year[monthIndex], this.Month[monthIndex], 1);
                    throw new NotSupportedException("Total monthly snowfall of " + totalSnowInMM + " mm in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN, negative, or exceeds the total monthly precipitation of " + totalPrecipitationInMM + " mm (time series chunk index " + monthIndex + ").");
                }
            }
        }
    }
}
