using iLand.Extensions;
using System;
using System.Globalization;

namespace iLand.Input.Weather
{
    public class CO2TimeSeriesMonthly : TimeSeries
    {
        public float[] CO2ConcentrationInPpm { get; private set; } // atmospheric CO₂ concentration

        public CO2TimeSeriesMonthly()
            : base(Timestep.Monthly)
        {
            this.CO2ConcentrationInPpm = Array.Empty<float>();

            // position time series year indices one year before the first year in the series so that they become valid on
            // the first call to OnStartYear()
            this.CurrentYearStartIndex = -Constant.MonthsInYear;
            this.NextYearStartIndex = 0;
        }

        public override void Resize(int newSize)
        {
            base.Resize(newSize);

            this.CO2ConcentrationInPpm = this.CO2ConcentrationInPpm.Resize(newSize);
        }

        public override void Validate(int startIndex, int months)
        {
            base.Validate(startIndex, months);

            int stopIndex = startIndex + months;
            for (int monthIndex = startIndex; monthIndex < stopIndex; ++monthIndex)
            {
                float co2concentration = this.CO2ConcentrationInPpm[monthIndex];
                if (Single.IsNaN(co2concentration) || (co2concentration < 0.0F) || (co2concentration > 2000.0F)) // for now, assume RCP 8.5 upper bound
                {
                    DateTime date = new(this.Year[monthIndex], this.Month[monthIndex], 1);
                    throw new NotSupportedException("Atmospheric CO₂ concentration of " + co2concentration + " ppm in " + date.ToString("MMM yyyy", CultureInfo.CurrentUICulture) + " is NaN, negative, or unexpectedly high (time series chunk index " + monthIndex + ").");
                }
            }
        }
    }
}
