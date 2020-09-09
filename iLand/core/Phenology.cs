using iLand.tools;
using System.Diagnostics;

namespace iLand.core
{
    internal class Phenology
    {
        private int mId; ///< identifier of this Phenology group
        private Climate mClimate; ///< link to relevant climate source
        private double mMinVpd; ///< minimum vpd [kPa]
        private double mMaxVpd; ///< maximum vpd [kPa]
        private double mMinDayLength; ///< minimum daylength [hours]
        private double mMaxDayLength; ///< maximum daylength [hours]
        private double mMinTemp; ///< minimum temperature [deg]
        private double mMaxTemp; ///< maximum temperature [deg]
        private double[] mPhenoFraction; ///< fraction [0..1] of month i [0..11] to are inside the vegetation period, i.e. have leafs
        private int mDayStart; ///< start of vegetation period (in day of year)
        private int mDayEnd; ///< end of vegetation period (in days of year, 1.1. = 0)
        private int mChillDaysBefore, mChillDaysAfter; ///< number of days that meet chilling requirements (>-5 deg C, <+5 deg C) before and after the vegetation period in this yeaer
        private int mChillDaysAfterLastYear; ///< chilling days of the last years autumn/winter


        /// get result of phenology calcualtion for this year (a pointer to a array of 12 values between 0..1: 0: no days with foliage)
        public double[] month() { return mPhenoFraction; }
        public int vegetationPeriodLength() { return mDayEnd - mDayStart; } ///< length of vegetation period in days, returs 365 for evergreens
        public int vegetationPeriodStart() { return mDayStart; } ///< day of year when vegeation period starts
        public int vegetationPeriodEnd() { return mDayEnd; } ///< day of year when vegeation period stops
        // chilling days
        /// get days of year that meet chilling requirements: the days in the autumn of the last year + the days of this spring season
        public int chillingDays() { return mChillDaysBefore + mChillDaysAfterLastYear; }
        public int chillingDaysLastYear() { return mChillDaysAfterLastYear; }

        public Phenology(Climate climate)
        {
            mClimate = climate;
            mId = 0;
            mMinVpd = mMaxVpd = mMinDayLength = mMaxDayLength = mMinTemp = mMaxTemp = 0.0;
            mDayStart = 0;
            mDayEnd = 365; mChillDaysBefore = -1; mChillDaysAfter = 0; mChillDaysAfterLastYear = 0;
            mPhenoFraction = new double[12];
        }

        public Phenology(int id, Climate climate, double minVpd, double maxVpd, double minDayLength, double maxDayLength,
                         double minTemp, double maxTemp)
            : this(climate)
        {
            mId = id;
            mMinVpd = minVpd;
            mMaxVpd = maxVpd;
            mMinDayLength = minDayLength;
            mMaxDayLength = maxDayLength;
            mMinTemp = minTemp;
            mMaxTemp = maxTemp;
            mChillDaysAfter = 0;
            mChillDaysAfterLastYear = 0;
        }

        public int id() { return mId; }

        // some special calculations used for establishment
        private void calculateChillDays(int end_of_season = -1)
        {
            int iday = 0;
            mChillDaysBefore = 0;
            int days_after = 0;
            int last_day = end_of_season > 0 ? end_of_season : mDayEnd;
            for (int index = mClimate.begin(); index != mClimate.end(); ++index, ++iday)
            {
                ClimateDay day = mClimate[index];
                if (day.temperature >= -5.0 && day.temperature < 5.0)
                {
                    if (iday < mDayStart)
                    {
                        mChillDaysBefore++;
                    }
                    if (iday > last_day)
                    {
                        days_after++;
                    }
                }
            }
            if (GlobalSettings.instance().currentYear() == 1)
            {
                // for the first simulation year, use the value of this autumn for the last years autumn
                mChillDaysAfterLastYear = days_after;
            }
            else
            {
                mChillDaysAfterLastYear = mChillDaysAfter;
            }
            mChillDaysAfter = days_after;
        }


        /// calculate the phenology for the current year
        public void calculate()
        {
            if (id() == 0)
            {
                // for needles: just calculate the chilling requirement for the establishment
                // i.e.: use the "bottom line" of 10.5 hrs daylength for the end of the vegetation season
                calculateChillDays(mClimate.sun().dayShorter10_5hrs());
                return;
            }
            double vpd, temp, daylength;
            double gsi; // combined factor of effect of vpd, temperature and day length
            bool inside_period = !mClimate.sun().northernHemishere(); // on northern hemisphere 1.1. is in winter
            int day_start = -1, day_stop = -1;
            int day_wait_for = -1;

            int iday = 0;
            for (int index = mClimate.begin(); index != mClimate.end(); ++index, ++iday)
            {
                ClimateDay day = mClimate[index];
                if (day_wait_for >= 0 && iday < day_wait_for)
                {
                    continue;
                }
                vpd = 1.0 - ramp(day.vpd, mMinVpd, mMaxVpd); // high value for low vpd
                temp = ramp(day.min_temperature, mMinTemp, mMaxTemp);
                daylength = ramp(mClimate.sun().daylength(iday), mMinDayLength, mMaxDayLength);
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
                    day_wait_for = mClimate.sun().longestDay();
                }
                else if (inside_period && gsi < 0.5)
                {
                    // switch from summer to winter
                    day_stop = iday;
                    if (day_start != -1)
                    {
                        break; // finished
                    }
                    day_wait_for = mClimate.sun().longestDay();
                    inside_period = false;
                }
            }
            day_start -= 10; // three-week-floating average: subtract 10 days
            day_stop -= 10;
            if (day_start < -1 || day_stop < -1)
            {
                //throw IException(QString("Phenology::calculation(): was not able to determine the length of the vegetation period for group %1. climate table: '%2'." ).arg(id()).arg(mClimate.name()));
                Debug.WriteLine("Phenology::calculation(): vegetation period is 0 for group" + id() + ", climate table: " + mClimate.name());
                day_start = mClimate.daysOfYear() - 1; // last day of the year, never reached
                day_stop = day_start; // never reached
            }
            if (GlobalSettings.instance().logLevelDebug())
            {
                Debug.WriteLine("Jolly-phenology. start " + mClimate.dayOfYear(day_start) + " stop " + mClimate.dayOfYear(day_stop));
            }
            mDayStart = day_start;
            mDayEnd = day_stop;
            // convert yeardays to dates
            mClimate.toDate(day_start, out int bDay, out int bMon, out int _);
            mClimate.toDate(day_stop, out int eDay, out int eMon, out int _);
            for (int i = 0; i < 12; i++)
            {
                if (i < bMon || i > eMon)
                {
                    mPhenoFraction[i] = 0; // out of season
                }
                else if (i > bMon && i < eMon)
                {
                    mPhenoFraction[i] = 1.0; // full inside of season
                }
                else
                {
                    // fractions of month
                    mPhenoFraction[i] = 1.0;
                    if (i == bMon)
                    {
                        mPhenoFraction[i] -= (bDay + 1) / mClimate.days(bMon);
                    }
                    if (i == eMon)
                    {
                        mPhenoFraction[i] -= (mClimate.days(eMon) - (bDay + 1)) / mClimate.days(eMon);
                    }
                }
            }

            calculateChillDays();
        }

        private double ramp(double value, double minValue, double maxValue)
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
