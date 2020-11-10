using System.Diagnostics;

namespace iLand.World
{
    // assumes calendar year aligned simulation years and northern hemisphere leaf on to leaf off period occuring within a single simulation year
    // TODO: support southern hemisphere sites
    public class Phenology
    {
        private readonly Climate mClimate; // link to relevant climate source
        private readonly float mMinVpd; // minimum vpd [kPa]
        private readonly float mMaxVpd; // maximum vpd [kPa]
        private readonly float mMinDayLength; // minimum daylength [hours]
        private readonly float mMaxDayLength; // maximum daylength [hours]
        private readonly float mMinTemp; // minimum temperature [deg]
        private readonly float mMaxTemp; // maximum temperature [deg]
        private int mChillDaysBeforeLeafOn, mChillDaysAfterLeafOff; // number of days that meet chilling requirements (>-5 deg C, <+5 deg C) before and after the vegetation period in this yeaer

        public int ChillingDaysAfterLeafOffInPreviousYear { get; private set; }
        public int LeafType { get; private set; } // identifier of this Phenology group
        /// get result of phenology calculation for this year (a pointer to a array of 12 values between 0..1: 0: no days with foliage)
        public float[] LeafOnFraction { get; private set; }
        public int LeafOnStart { get; private set; } // day of year when vegeation period starts
        public int LeafOnEnd { get; private set; } // day of year when vegeation period stops

        public int GetLeafOnDurationInDays() { return this.LeafOnEnd - this.LeafOnStart; } // length of vegetation period in days, returns 365 for evergreens
        /// get days of year that meet chilling requirements: the days in the autumn of the last year + the days of this spring season
        public int GetWinterChillingDays() { return this.mChillDaysBeforeLeafOn + this.ChillingDaysAfterLeafOffInPreviousYear; }

        // 
        public Phenology(Climate climate)
        {
            this.mChillDaysBeforeLeafOn = -1;
            this.mChillDaysAfterLeafOff = -1;
            this.mClimate = climate;
            this.mMinVpd = mMaxVpd = mMinDayLength = mMaxDayLength = mMinTemp = mMaxTemp = 0.0F;

            this.ChillingDaysAfterLeafOffInPreviousYear = 0;
            this.LeafOnStart = 0;
            this.LeafOnEnd = 365; 
            this.LeafOnFraction = new float[12];
            this.LeafType = 0;
        }

        public Phenology(int id, Climate climate, float minVpd, float maxVpd, float minDayLength, float maxDayLength,
                         float minTemp, float maxTemp)
            : this(climate)
        {
            LeafType = id;
            mMinVpd = minVpd;
            mMaxVpd = maxVpd;
            mMinDayLength = minDayLength;
            mMaxDayLength = maxDayLength;
            mMinTemp = minTemp;
            mMaxTemp = maxTemp;
            mChillDaysAfterLeafOff = 0;
            ChillingDaysAfterLeafOffInPreviousYear = 0;
        }

        // some special calculations used for establishment
        private void CalculateChillDays(int leafOffDay = -1)
        {
            this.mChillDaysBeforeLeafOn = 0;
            int chillDaysAfterLeafOff = 0;
            int lastDayWithLeaves = leafOffDay > 0 ? leafOffDay : this.LeafOnEnd;
            for (int dayOfYear = 0, index = mClimate.CurrentJanuary1; index != mClimate.NextJanuary1; ++dayOfYear, ++index)
            {
                ClimateDay day = mClimate[index];
                if (day.MeanDaytimeTemperature >= -5.0 && day.MeanDaytimeTemperature < 5.0)
                {
                    if (dayOfYear < this.LeafOnStart)
                    {
                        ++this.mChillDaysBeforeLeafOn;
                    }
                    if (dayOfYear > lastDayWithLeaves)
                    {
                        ++chillDaysAfterLeafOff;
                    }
                }
            }
            if (this.mChillDaysAfterLeafOff < 0)
            {
                // for the first simulation year, use the value of this autumn as an approximation of the previous year's autumn
                this.ChillingDaysAfterLeafOffInPreviousYear = chillDaysAfterLeafOff;
            }
            else
            {
                this.ChillingDaysAfterLeafOffInPreviousYear = this.mChillDaysAfterLeafOff;
            }

            this.mChillDaysAfterLeafOff = chillDaysAfterLeafOff;
        }

        /// calculate the phenology for the current year
        public void RunYear()
        {
            // TODO: change ID to type enum
            if (this.LeafType == 0)
            {
                // for needles: just calculate the chilling requirement for the establishment
                // i.e.: use the "bottom line" of 10.5 hrs daylength for the end of the vegetation season
                // TODO: why does vegetation season cut off at this day length for conifers?
                this.CalculateChillDays(mClimate.Sun.LastDayLongerThan10_5Hours);
                return;
            }

            bool inside_period = !mClimate.Sun.IsNorthernHemisphere(); // on northern hemisphere 1.1. is in winter
            int day_start = -1, day_stop = -1;
            int day_wait_for = -1;

            for (int dayOfYear = 0, index = mClimate.CurrentJanuary1; index != mClimate.NextJanuary1; ++dayOfYear, ++index)
            {
                ClimateDay day = mClimate[index];
                if (day_wait_for >= 0 && dayOfYear < day_wait_for)
                {
                    continue;
                }
                float vpdFactor = 1.0F - Phenology.GetRelativePositionInRange(day.Vpd, mMinVpd, mMaxVpd); // high value for low vpd
                float tempFactor = Phenology.GetRelativePositionInRange(day.MinTemperature, mMinTemp, mMaxTemp);
                float dayLengthFactor = Phenology.GetRelativePositionInRange(mClimate.Sun.GetDayLengthInHours(dayOfYear), mMinDayLength, mMaxDayLength);
                float gsi = vpdFactor * tempFactor * dayLengthFactor; // combined factor of effect of vpd, temperature and day length
                if (!inside_period && gsi > 0.5)
                {
                    // switch from winter -> summer
                    inside_period = true;
                    day_start = dayOfYear;
                    if (day_stop != -1)
                    {
                        break;
                    }
                    day_wait_for = mClimate.Sun.LongestDay;
                }
                else if (inside_period && gsi < 0.5)
                {
                    // switch from summer to winter
                    day_stop = dayOfYear;
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
                Debug.WriteLine("Phenology::calculation(): vegetation period is 0 for group " + LeafType + ", climate table: " + mClimate.Name);
                day_start = mClimate.GetDaysInYear() - 1; // last day of the year, never reached
                day_stop = day_start; // never reached
            }
            //if (GlobalSettings.Instance.LogDebug())
            //{
            //    Debug.WriteLine("Jolly-phenology. start " + mClimate.DayOfYear(day_start) + " stop " + mClimate.DayOfYear(day_stop));
            //}
            this.LeafOnStart = day_start;
            this.LeafOnEnd = day_stop;
            // convert yeardays to dates
            mClimate.ToZeroBasedDate(day_start, out int bDay, out int leafOnMonth, out int _);
            mClimate.ToZeroBasedDate(day_stop, out int _, out int leafOffMonth, out int _);
            for (int month = 0; month < 12; ++month)
            {
                if (month < leafOnMonth || month > leafOffMonth)
                {
                    this.LeafOnFraction[month] = 0.0F; // out of season
                }
                else if (month > leafOnMonth && month < leafOffMonth)
                {
                    this.LeafOnFraction[month] = 1.0F; // full inside of season
                }
                else
                {
                    // fractions of month
                    this.LeafOnFraction[month] = 1.0F;
                    if (month == leafOnMonth)
                    {
                        this.LeafOnFraction[month] -= (bDay + 1) / mClimate.GetDaysInMonth(leafOnMonth);
                    }
                    if (month == leafOffMonth)
                    {
                        this.LeafOnFraction[month] -= (mClimate.GetDaysInMonth(leafOffMonth) - (bDay + 1)) / mClimate.GetDaysInMonth(leafOffMonth);
                    }
                }
            }

            this.CalculateChillDays();
        }

        private static float GetRelativePositionInRange(float value, float minValue, float maxValue)
        {
            if (value < minValue)
            {
                return 0.0F;
            }
            if (value > maxValue)
            {
                return 1.0F;
            }
            return (value - minValue) / (maxValue - minValue);
        }
    }
}
