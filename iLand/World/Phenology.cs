using iLand.Input;

namespace iLand.World
{
    // assumes calendar year aligned simulation years and northern hemisphere leaf on to leaf off period occuring within a single simulation year
    // TODO: support southern hemisphere sites
    public class Phenology
    {
        private readonly WeatherDaily weather; // link to relevant climate source
        private readonly float minVpd; // minimum vpd [kPa]
        private readonly float maxVpd; // maximum vpd [kPa]
        private readonly float minDayLength; // minimum daylength [hours]
        private readonly float maxDayLength; // maximum daylength [hours]
        private readonly float minTemp; // minimum temperature [deg]
        private readonly float maxTemp; // maximum temperature [deg]
        private int chillDaysBeforeLeafOn; // number of days that meet chilling requirements (>-5 deg C, <+5 deg C) before and after the vegetation period in this yeaer
        private int chillDaysAfterLeafOff;

        public int ChillingDaysAfterLeafOffInPreviousYear { get; private set; }
        public int LeafType { get; private init; } // identifier of this Phenology group
        // get result of phenology calculation for this year (a pointer to a array of 12 values between 0..1: 0 = no days with foliage)
        // BUGBUG: set but not consumed
        public float[] LeafOnFraction { get; private init; }
        public int LeafOnStart { get; private set; } // day of year when vegeation period starts
        public int LeafOnEnd { get; private set; } // day of year when vegeation period stops

        public int GetLeafOnDurationInDays() { return this.LeafOnEnd - this.LeafOnStart; } // length of vegetation period in days, returns 365 for evergreens
        // get days of year that meet chilling requirements: the days in the autumn of the last year + the days of this spring season
        public int GetWinterChillingDays() { return this.chillDaysBeforeLeafOn + this.ChillingDaysAfterLeafOffInPreviousYear; }

        public Phenology(WeatherDaily weather)
        {
            this.chillDaysBeforeLeafOn = -1;
            this.chillDaysAfterLeafOff = -1;
            this.weather = weather;
            this.minVpd = 0.0F;
            this.maxVpd = 0.0F;
            this.minDayLength = 0.0F;
            this.maxDayLength = 0.0F;
            this.minTemp = 0.0F;
            this.maxTemp = 0.0F;

            this.ChillingDaysAfterLeafOffInPreviousYear = 0;
            this.LeafOnStart = 0;
            this.LeafOnEnd = 365; 
            this.LeafOnFraction = new float[12];
            this.LeafType = 0;
        }

        public Phenology(int id, WeatherDaily weather, float minVpd, float maxVpd, float minDayLength, float maxDayLength,
                         float minTemp, float maxTemp)
            : this(weather)
        {
            this.LeafType = id;
            this.minVpd = minVpd;
            this.maxVpd = maxVpd;
            this.minDayLength = minDayLength;
            this.maxDayLength = maxDayLength;
            this.minTemp = minTemp;
            this.maxTemp = maxTemp;
            this.chillDaysAfterLeafOff = 0;
            this.ChillingDaysAfterLeafOffInPreviousYear = 0;
        }

        // some special calculations used for establishment
        private void CalculateChillDays(int leafOffDay = -1)
        {
            this.chillDaysBeforeLeafOn = 0;
            int chillDaysAfterLeafOff = 0;
            WeatherTimeSeriesDaily dailyWeather = this.weather.TimeSeries;
            int lastDayWithLeaves = leafOffDay > 0 ? leafOffDay : this.LeafOnEnd;
            for (int dayOfYear = 0, dayIndex = this.weather.CurrentJanuary1; dayIndex != this.weather.NextJanuary1; ++dayOfYear, ++dayIndex)
            {
                if ((dailyWeather.TemperatureDaytimeMean[dayIndex] >= -5.0F) && (dailyWeather.TemperatureDaytimeMean[dayIndex] < 5.0F))
                {
                    if (dayOfYear < this.LeafOnStart)
                    {
                        ++this.chillDaysBeforeLeafOn;
                    }
                    if (dayOfYear > lastDayWithLeaves)
                    {
                        ++chillDaysAfterLeafOff;
                    }
                }
            }
            if (this.chillDaysAfterLeafOff < 0)
            {
                // for the first simulation year, use the value of this autumn as an approximation of the previous year's autumn
                this.ChillingDaysAfterLeafOffInPreviousYear = chillDaysAfterLeafOff;
            }
            else
            {
                this.ChillingDaysAfterLeafOffInPreviousYear = this.chillDaysAfterLeafOff;
            }

            this.chillDaysAfterLeafOff = chillDaysAfterLeafOff;
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
                this.CalculateChillDays(this.weather.Sun.LastDayLongerThan10_5Hours);
                return;
            }

            WeatherTimeSeriesDaily dailyWeather = this.weather.TimeSeries;
            bool inLeafOnPeriod = !this.weather.Sun.IsNorthernHemisphere(); // on northern hemisphere January 1 is in winter
            int leafOnStartDay = -1;
            int leafOnEndDay = -1;
            int dayWaitFor = -1;
            for (int dayOfYear = 0, dayIndex = this.weather.CurrentJanuary1; dayIndex != this.weather.NextJanuary1; ++dayOfYear, ++dayIndex)
            {
                if (dayWaitFor >= 0 && dayOfYear < dayWaitFor)
                {
                    continue;
                }
                float vpdFactor = 1.0F - Phenology.GetRelativePositionInRange(dailyWeather.VpdMeanInKPa[dayIndex], this.minVpd, this.maxVpd); // high value for low vpd
                float tempFactor = Phenology.GetRelativePositionInRange(dailyWeather.TemperatureMin[dayIndex], this.minTemp, this.maxTemp);
                float dayLengthFactor = Phenology.GetRelativePositionInRange(this.weather.Sun.GetDayLengthInHours(dayOfYear), this.minDayLength, this.maxDayLength);
                float gsi = vpdFactor * tempFactor * dayLengthFactor; // combined factor of effect of vpd, temperature and day length
                if (!inLeafOnPeriod && (gsi > 0.5F))
                {
                    // switch from winter -> summer
                    inLeafOnPeriod = true;
                    leafOnStartDay = dayOfYear;
                    if (leafOnEndDay != -1)
                    {
                        break;
                    }
                    dayWaitFor = this.weather.Sun.LongestDay;
                }
                else if (inLeafOnPeriod && gsi < 0.5)
                {
                    // switch from summer to winter
                    leafOnEndDay = dayOfYear;
                    if (leafOnStartDay != -1)
                    {
                        break; // finished
                    }
                    dayWaitFor = this.weather.Sun.LongestDay;
                    inLeafOnPeriod = false;
                }
            }
            leafOnStartDay -= 10; // three-week-floating average: subtract 10 days
            leafOnEndDay -= 10;
            if (leafOnStartDay < -1 || leafOnEndDay < -1)
            {
                // throw IException(QString("Phenology::calculation(): was not able to determine the length of the vegetation period for group {0}. weather table: '{1}'.", id(), weather.name()));
                // Debug.WriteLine("Phenology::calculation(): vegetation period is 0 for group " + LeafType + ", weather table: " + weather.Name);
                leafOnStartDay = this.weather.GetDaysInYear() - 1; // last day of the year, never reached
                leafOnEndDay = leafOnStartDay; // never reached
            }
            //if (GlobalSettings.Instance.LogDebug())
            //{
            //    Debug.WriteLine("Jolly-phenology. start " + weather.DayOfYear(day_start) + " stop " + weather.DayOfYear(day_stop));
            //}
            this.LeafOnStart = leafOnStartDay;
            this.LeafOnEnd = leafOnEndDay;
            // convert yeardays to dates
            this.weather.ToZeroBasedDate(leafOnStartDay, out int leafOnDayIndex, out int leafOnMonthIndex);
            this.weather.ToZeroBasedDate(leafOnEndDay, out int _, out int leafOffMonthIndex);
            for (int month = 0; month < 12; ++month)
            {
                if ((month < leafOnMonthIndex) || (month > leafOffMonthIndex))
                {
                    this.LeafOnFraction[month] = 0.0F; // out of season
                }
                else if ((month > leafOnMonthIndex) && (month < leafOffMonthIndex))
                {
                    this.LeafOnFraction[month] = 1.0F; // full inside of season
                }
                else
                {
                    // fractions of month
                    this.LeafOnFraction[month] = 1.0F;
                    if (month == leafOnMonthIndex)
                    {
                        this.LeafOnFraction[month] -= (leafOnDayIndex + 1) / this.weather.GetDaysInMonth(leafOnMonthIndex);
                    }
                    if (month == leafOffMonthIndex)
                    {
                        this.LeafOnFraction[month] -= (this.weather.GetDaysInMonth(leafOffMonthIndex) - (leafOnDayIndex + 1)) / this.weather.GetDaysInMonth(leafOffMonthIndex);
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
