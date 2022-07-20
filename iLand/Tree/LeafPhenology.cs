using iLand.Input;
using iLand.Tool;
using iLand.World;
using System;
using System.Diagnostics;

namespace iLand.Tree
{
    /// <summary>
    /// Finds leaf on and off dates for deciduous species. Currently assumes calendar year aligned simulation years and a northern hemisphere 
    /// leaf on to leaf off period occuring within a single calendar year.
    /// </summary>
    /// <remarks>
    /// https://iland-model.org/phenology
    /// Jolly WM, Nemani R, Running SW. 2005. A generalized, bioclimatic index to predict foliar phenology in response to climate. Global Change
    ///   Biology 11(4):619-632. https://doi.org/10.1111/j.1365-2486.2005.00930.x
    /// </remarks>
    public abstract class LeafPhenology
    {
        public int ID { get; private init; } // identifier of this Phenology group
        // get result of phenology calculation for this year (a pointer to a array of 12 values between 0..1: 0 = no days with foliage)
        public float[] LeafOnFractionByMonth { get; private init; }
        public int LeafOnStartDayOfYearIndex { get; protected set; } // day of year when vegetation period starts
        public int LeafOnEndDayOfYearIndex { get; protected set; } // day of year when vegetation period stops

        protected LeafPhenology(int id, int leafOnStartDayIndex, int leafOnEndDayIndex)
        {
            if (id < Constant.EvergreenLeafPhenologyID)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }
            if ((leafOnStartDayIndex < 0) || (leafOnStartDayIndex > Constant.DaysInLeapYear))
            {
                throw new ArgumentOutOfRangeException(nameof(leafOnStartDayIndex));
            }
            if ((leafOnEndDayIndex < 0) || (leafOnEndDayIndex > Constant.DaysInLeapYear))
            {
                throw new ArgumentOutOfRangeException(nameof(leafOnEndDayIndex));
            }
            // northern hemisphere: leafOnStartDayIndex < leafOnEndDayIndex
            // southern hemisphere: leafOnStartDayIndex > leafOnEndDayIndex

            this.ID = id;
            this.LeafOnFractionByMonth = new float[Constant.MonthsInYear]; // left as 0 since populated in RunYear()
            this.LeafOnStartDayOfYearIndex = leafOnStartDayIndex;
            this.LeafOnEndDayOfYearIndex = leafOnEndDayIndex;
        }

        public bool IsEvergreen
        {
            get { return this.ID == Constant.EvergreenLeafPhenologyID; }
        }

        // calculate the phenology for the current year
        public abstract void GetLeafOnAndOffDatesForCurrentYear();

        // length of vegetation period in days, returns full length of year for evergreens
        public int GetLeafOnDurationInDays()
        {
            int leafOnDuration = this.LeafOnEndDayOfYearIndex - this.LeafOnStartDayOfYearIndex;
            Debug.Assert(leafOnDuration > 0);
            return leafOnDuration;
        }
    }

    public class LeafPhenology<TWeatherTimeSeries> : LeafPhenology where TWeatherTimeSeries : WeatherTimeSeries
    {
        private readonly float maxDayLength; // maximum day length, hours
        private readonly float maxTemp; // maximum temperature, °C
        private readonly float maxVpd; // maximum vpd, kPa
        private readonly float minDayLength; // minimum day length, hours
        private readonly float minTemp; // minimum temperature, °C
        private readonly float minVpd; // minimum vpd, kPa
        private readonly Weather<TWeatherTimeSeries> weather; // link to relevant weather driving leaf on and off dates

        protected LeafPhenology(Weather<TWeatherTimeSeries> weather, int id, float minVpd, float maxVpd, float minDayLength, float maxDayLength, float minTemp, float maxTemp)
            : base(id, 0, Constant.DaysInLeapYear) // default to leaves always on
        {
            this.minVpd = minVpd;
            this.maxVpd = maxVpd;
            this.minDayLength = minDayLength;
            this.maxDayLength = maxDayLength;
            this.minTemp = minTemp;
            this.maxTemp = maxTemp;
            this.weather = weather;
        }

        public LeafPhenology(Weather<TWeatherTimeSeries> weather, Input.ProjectFile.LeafPhenology phenology)
            : this(weather, phenology.ID, phenology.VpdMin, phenology.VpdMax, phenology.DayLengthMin, phenology.DayLengthMax, phenology.TempMin, phenology.TempMax)
        {
        }

        public static LeafPhenology<TWeatherTimeSeries> CreateEvergreen(Weather<TWeatherTimeSeries> weather)
        {
            return new(weather, Constant.EvergreenLeafPhenologyID, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
        }

        public override void GetLeafOnAndOffDatesForCurrentYear()
        {
            // TODO: change ID to string? (or enum?)
            if (this.ID == Constant.EvergreenLeafPhenologyID)
            {
                // special case default evergreen phenology: nothing to do since leaves are always on
                return;
            }

            // find deciduous species' start and end of leaf on period in this year
            // TODO: support southern hemisphere sites
            TWeatherTimeSeries weatherTimeSeries = this.weather.TimeSeries;
            Debug.Assert(weatherTimeSeries.Timestep == Timestep.Daily);
            // for now, assume January 1 is aways leaf off in the northern hemisphere and leaf on in the southern hemisphere
            // TODO: how fragile are this assumption and the assumption about skipping to the longest day of the year?
            bool inLeafOnPeriod = !this.weather.Sun.IsNorthernHemisphere;
            int leafOnStartDayIndex = -1;
            int leafOnEndDayIndex = -1;
            int longestDayOfYearIndex = -1;
            for (int weatherTimestepIndex = 0, dayIndex = weatherTimeSeries.CurrentYearStartIndex; dayIndex != weatherTimeSeries.NextYearStartIndex; ++weatherTimestepIndex, ++dayIndex)
            {
                if ((longestDayOfYearIndex >= 0) && (weatherTimestepIndex < longestDayOfYearIndex))
                {
                    continue;
                }

                float vpdModifier = 1.0F - LeafPhenology<TWeatherTimeSeries>.GetRelativePositionInRange(weatherTimeSeries.VpdMeanInKPa[dayIndex], this.minVpd, this.maxVpd); // high value for low vpd
                float temperatureModifier = LeafPhenology<TWeatherTimeSeries>.GetRelativePositionInRange(weatherTimeSeries.TemperatureMin[dayIndex], this.minTemp, this.maxTemp);
                float dayLengthModifier = LeafPhenology<TWeatherTimeSeries>.GetRelativePositionInRange(weather.Sun.GetDayLengthInHours(weatherTimestepIndex), this.minDayLength, this.maxDayLength);
                float establishmentModifier = vpdModifier * temperatureModifier * dayLengthModifier; // Jolly et al. 2005 GSI (growing season index): combined factor of effect of vpd, temperature and day length
                
                if (!inLeafOnPeriod && (establishmentModifier > 0.5F))
                {
                    // switch from leaf off to leaf on
                    inLeafOnPeriod = true;
                    leafOnStartDayIndex = weatherTimestepIndex;
                    if (leafOnEndDayIndex != -1)
                    {
                        break; // finished: found both leaf on and leaf off dates
                    }
                    longestDayOfYearIndex = weather.Sun.LongestDayIndex;
                }
                else if (inLeafOnPeriod && (establishmentModifier < 0.5))
                {
                    // switch from leaf on to leaf off
                    leafOnEndDayIndex = weatherTimestepIndex;
                    if (leafOnStartDayIndex != -1)
                    {
                        break; // finished: found both leaf on and leaf off dates
                    }
                    longestDayOfYearIndex = weather.Sun.LongestDayIndex;
                    inLeafOnPeriod = false;
                }
            }

            bool isLeapYear = weatherTimeSeries.IsCurrentlyLeapYear();
            leafOnStartDayIndex -= 10; // three-week-floating average: subtract 10 days
            leafOnEndDayIndex -= 10;
            if (leafOnStartDayIndex < -1 || leafOnEndDayIndex < -1)
            {
                // throw IException(QString("Phenology::calculation(): was not able to determine the length of the vegetation period for group {0}. weather table: '{1}'.", id(), weather.name()));
                // Debug.WriteLine("Phenology::calculation(): vegetation period is 0 for group " + LeafType + ", weather table: " + weather.Name);
                leafOnStartDayIndex = DateTimeExtensions.GetDaysInYear(isLeapYear) - 1; // last day of the year, never reached
                leafOnEndDayIndex = leafOnStartDayIndex; // never reached
            }

            this.LeafOnStartDayOfYearIndex = leafOnStartDayIndex;
            this.LeafOnEndDayOfYearIndex = leafOnEndDayIndex;

            // convert day of year to dates
            (int leafOnDayOfMonthIndex, int leafOnMonthIndex) = DateTimeExtensions.DayOfYearToDayOfMonth(leafOnStartDayIndex);
            (int leafOffDayOfMonthIndex, int leafOffMonthIndex) = DateTimeExtensions.DayOfYearToDayOfMonth(leafOnEndDayIndex);
            for (int monthIndex = 0; monthIndex < 12; ++monthIndex)
            {
                if (monthIndex < leafOnMonthIndex || monthIndex > leafOffMonthIndex)
                {
                    this.LeafOnFractionByMonth[monthIndex] = 0.0F; // whole month is leaf off
                }
                else if (monthIndex > leafOnMonthIndex && monthIndex < leafOffMonthIndex)
                {
                    this.LeafOnFractionByMonth[monthIndex] = 1.0F; // whole month is leaf on
                }
                else
                {
                    // fractions of month
                    float leafOnFraction = 1.0F;
                    float daysInMonth = (float)DateTimeExtensions.GetDaysInMonth(leafOnMonthIndex, isLeapYear);
                    if (monthIndex == leafOnMonthIndex)
                    {
                        leafOnFraction -= (leafOnDayOfMonthIndex + 1) / daysInMonth;
                    }
                    if (monthIndex == leafOffMonthIndex)
                    {
                        leafOnFraction -= (daysInMonth - (leafOffDayOfMonthIndex + 1)) / daysInMonth;
                    }

                    this.LeafOnFractionByMonth[monthIndex] = leafOnFraction;
                }
            }
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
