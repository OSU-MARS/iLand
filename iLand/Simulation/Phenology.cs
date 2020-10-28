using iLand.Tools;
using iLand.World;
using System.Diagnostics;

namespace iLand.Simulation
{
    public class Phenology
    {
        private readonly Climate mClimate; // link to relevant climate source
        private readonly double mMinVpd; // minimum vpd [kPa]
        private readonly double mMaxVpd; // maximum vpd [kPa]
        private readonly double mMinDayLength; // minimum daylength [hours]
        private readonly double mMaxDayLength; // maximum daylength [hours]
        private readonly double mMinTemp; // minimum temperature [deg]
        private readonly double mMaxTemp; // maximum temperature [deg]
        private int mChillDaysBefore, mChillDaysAfter; // number of days that meet chilling requirements (>-5 deg C, <+5 deg C) before and after the vegetation period in this yeaer

        public int ChillingDaysLastYear { get; private set; }
        public int ID { get; private set; } // identifier of this Phenology group
        /// get result of phenology calculation for this year (a pointer to a array of 12 values between 0..1: 0: no days with foliage)
        public double[] LeafOnFraction { get; private set; }
        public int LeafOnStart { get; private set; } // day of year when vegeation period starts
        public int LeafOnEnd { get; private set; } // day of year when vegeation period stops

        // chilling days
        /// get days of year that meet chilling requirements: the days in the autumn of the last year + the days of this spring season
        public int ChillingDays() { return mChillDaysBefore + ChillingDaysLastYear; }
        public int LeafOnDuration() { return LeafOnEnd - LeafOnStart; } // length of vegetation period in days, returs 365 for evergreens

        public Phenology(Climate climate)
        {
            mClimate = climate;
            ID = 0;
            mMinVpd = mMaxVpd = mMinDayLength = mMaxDayLength = mMinTemp = mMaxTemp = 0.0;
            LeafOnStart = 0;
            LeafOnEnd = 365; mChillDaysBefore = -1; mChillDaysAfter = 0; ChillingDaysLastYear = 0;
            LeafOnFraction = new double[12];
        }

        public Phenology(int id, Climate climate, double minVpd, double maxVpd, double minDayLength, double maxDayLength,
                         double minTemp, double maxTemp)
            : this(climate)
        {
            ID = id;
            mMinVpd = minVpd;
            mMaxVpd = maxVpd;
            mMinDayLength = minDayLength;
            mMaxDayLength = maxDayLength;
            mMinTemp = minTemp;
            mMaxTemp = maxTemp;
            mChillDaysAfter = 0;
            ChillingDaysLastYear = 0;
        }

        // some special calculations used for establishment
        private void CalculateChillDays(Model model, int end_of_season = -1)
        {
            int iday = 0;
            mChillDaysBefore = 0;
            int days_after = 0;
            int last_day = end_of_season > 0 ? end_of_season : LeafOnEnd;
            for (int index = mClimate.CurrentJanuary1; index != mClimate.NextJanuary1; ++index, ++iday)
            {
                ClimateDay day = mClimate[index];
                if (day.MeanDaytimeTemperature >= -5.0 && day.MeanDaytimeTemperature < 5.0)
                {
                    if (iday < LeafOnStart)
                    {
                        mChillDaysBefore++;
                    }
                    if (iday > last_day)
                    {
                        days_after++;
                    }
                }
            }
            if (model.ModelSettings.CurrentYear == 1)
            {
                // for the first simulation year, use the value of this autumn for the last years autumn
                ChillingDaysLastYear = days_after;
            }
            else
            {
                ChillingDaysLastYear = mChillDaysAfter;
            }
            mChillDaysAfter = days_after;
        }

        /// calculate the phenology for the current year
        public void Calculate(Model model)
        {
            if (ID == 0)
            {
                // for needles: just calculate the chilling requirement for the establishment
                // i.e.: use the "bottom line" of 10.5 hrs daylength for the end of the vegetation season
                CalculateChillDays(model, mClimate.Sun.LastDayLongerThan10_5Hours);
                return;
            }
            double vpd, temp, daylength;
            double gsi; // combined factor of effect of vpd, temperature and day length
            bool inside_period = !mClimate.Sun.NorthernHemisphere(); // on northern hemisphere 1.1. is in winter
            int day_start = -1, day_stop = -1;
            int day_wait_for = -1;

            int iday = 0;
            for (int index = mClimate.CurrentJanuary1; index != mClimate.NextJanuary1; ++index, ++iday)
            {
                ClimateDay day = mClimate[index];
                if (day_wait_for >= 0 && iday < day_wait_for)
                {
                    continue;
                }
                vpd = 1.0 - Ramp(day.Vpd, mMinVpd, mMaxVpd); // high value for low vpd
                temp = Ramp(day.MinTemperature, mMinTemp, mMaxTemp);
                daylength = Ramp(mClimate.Sun.GetDayLengthInHours(iday), mMinDayLength, mMaxDayLength);
                gsi = vpd * temp * daylength;
                if (!inside_period && gsi > 0.5)
                {
                    // switch from winter -> summer
                    inside_period = true;
                    day_start = iday;
                    if (day_stop != -1)
                    {
                        break;
                    }
                    day_wait_for = mClimate.Sun.LongestDay;
                }
                else if (inside_period && gsi < 0.5)
                {
                    // switch from summer to winter
                    day_stop = iday;
                    if (day_start != -1)
                    {
                        break; // finished
                    }
                    day_wait_for = mClimate.Sun.LongestDay;
                    inside_period = false;
                }
            }
            day_start -= 10; // three-week-floating average: subtract 10 days
            day_stop -= 10;
            if (day_start < -1 || day_stop < -1)
            {
                //throw IException(QString("Phenology::calculation(): was not able to determine the length of the vegetation period for group {0}. climate table: '{1}'.", id(), mClimate.name()));
                Debug.WriteLine("Phenology::calculation(): vegetation period is 0 for group " + ID + ", climate table: " + mClimate.Name);
                day_start = mClimate.GetDaysInYear() - 1; // last day of the year, never reached
                day_stop = day_start; // never reached
            }
            //if (GlobalSettings.Instance.LogDebug())
            //{
            //    Debug.WriteLine("Jolly-phenology. start " + mClimate.DayOfYear(day_start) + " stop " + mClimate.DayOfYear(day_stop));
            //}
            LeafOnStart = day_start;
            LeafOnEnd = day_stop;
            // convert yeardays to dates
            mClimate.ToZeroBasedDate(day_start, out int bDay, out int bMon, out int _);
            mClimate.ToZeroBasedDate(day_stop, out int _, out int eMon, out int _);
            for (int i = 0; i < 12; i++)
            {
                if (i < bMon || i > eMon)
                {
                    LeafOnFraction[i] = 0; // out of season
                }
                else if (i > bMon && i < eMon)
                {
                    LeafOnFraction[i] = 1.0; // full inside of season
                }
                else
                {
                    // fractions of month
                    LeafOnFraction[i] = 1.0;
                    if (i == bMon)
                    {
                        LeafOnFraction[i] -= (bDay + 1) / mClimate.GetDaysInMonth(bMon);
                    }
                    if (i == eMon)
                    {
                        LeafOnFraction[i] -= (mClimate.GetDaysInMonth(eMon) - (bDay + 1)) / mClimate.GetDaysInMonth(eMon);
                    }
                }
            }

            CalculateChillDays(model);
        }

        private double Ramp(double value, double minValue, double maxValue)
        {
            if (value < minValue)
            {
                return 0.0;
            }
            if (value > maxValue)
            {
                return 1.0;
            }
            return (value - minValue) / (maxValue - minValue);
        }
    }
}
