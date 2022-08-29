using iLand.Extensions;
using System;

namespace iLand.Input.Weather
{
    public class TimeSeries
    {
        public int Count { get; set; }
        public int Capacity { get; private set; }
        public Timestep Timestep { get; private init; }

        // index of the first day or month (depending on whether the series is daily or monthly) of the current year (simulation timestep)
        public int CurrentYearStartIndex { get; set; }
        // index of the first day or month the subsequent year; stop index for external iterations over days in year
        public int NextYearStartIndex { get; set; }

        // Gregorian calendar year, CE
        public Int16[] Year { get; private set; }
        // month (1..12)
        public byte[] Month { get; private set; }

        protected TimeSeries(Timestep timestep)
        {
            this.Capacity = 0;
            this.Count = 0;
            this.Timestep = timestep;

            this.CurrentYearStartIndex = -1;
            this.NextYearStartIndex = -1;

            this.Month = Array.Empty<byte>();
            this.Year = Array.Empty<Int16>(); // can be shortened to one element per year if needed
        }

        public virtual void Resize(int newSize)
        {
            this.Capacity = newSize;

            this.Month = this.Month.Resize(newSize);
            this.Year = this.Year.Resize(newSize);
        }

        public virtual void Validate(int startIndex, int count)
        {
            int endIndex = startIndex + count;
            for (int index = startIndex; index < endIndex; ++index)
            {
                int year = this.Year[index];
                if ((year < Constant.Limit.YearMin) || (year > Constant.Limit.YearMax))
                {
                    // not necessary but avoids failures if a DateTime needs to be constructed for this point in the time series
                    throw new NotSupportedException("Year " + year + " is unexpectedly far in the past or the future (at time series chunk index " + index + ").");
                }

                int month = this.Month[index];
                if ((month < 1) || (month > Constant.Time.MonthsInYear))
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
            }
        }
    }
}
