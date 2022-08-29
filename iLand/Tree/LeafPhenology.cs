using iLand.Extensions;
using iLand.Input;
using iLand.Input.Weather;
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
        // TODO: change ID to string? (or enum?)
        public int ID { get; private init; } // identifier of this phenology group
        // get result of phenology calculation for this year (a pointer to a array of 12 values between 0..1: 0 = no days with foliage)
        public float[] LeafOnFractionByMonth { get; private init; }

        // northern hemisphere: leafOnStartDayIndex < leafOnEndDayIndex
        // southern hemisphere: leafOnStartDayIndex > leafOnEndDayIndex
        public int LeafOnStartDayOfYearIndex { get; protected set; } // day of year when leaf on period starts
        public int LeafOnEndDayOfYearIndex { get; protected set; } // day of year when leaf on period stops

        protected LeafPhenology(int id)
        {
            if (id < Constant.EvergreenLeafPhenologyID)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            this.ID = id;
            this.LeafOnFractionByMonth = new float[Constant.Time.MonthsInYear]; // left as 0 since populated in RunYear()
            this.LeafOnStartDayOfYearIndex = 0; // default to leaf on at start of year
            this.LeafOnEndDayOfYearIndex = Constant.Time.DaysInYear; // updated in RunYear() for leap years

            if (id == Constant.EvergreenLeafPhenologyID)
            {
                Array.Fill(this.LeafOnFractionByMonth, 1.0F); // never changes, so set once here
            }
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
            : base(id)
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
            return new(weather, Constant.EvergreenLeafPhenologyID, Single.NaN, Single.NaN, Single.NaN, Single.NaN, Single.NaN, Single.NaN);
        }

        public override void GetLeafOnAndOffDatesForCurrentYear()
        {
            TWeatherTimeSeries weatherTimeSeries = this.weather.TimeSeries;
            bool isLeapYear = weatherTimeSeries.IsCurrentlyLeapYear();
            // special case for evergreen phenology: start index and leaf on fractions don't change so only update needed is for end of year
            if (this.ID == Constant.EvergreenLeafPhenologyID)
            {
                this.LeafOnEndDayOfYearIndex = DateTimeExtensions.GetDaysInYear(isLeapYear);
                return;
            }

            // find deciduous species' start and end of leaf on period in this year
            // TODO: support southern hemisphere sites
            // for now, assume January 1 is aways leaf off in the northern hemisphere and leaf on in the southern hemisphere
            // TODO: how fragile are this assumption and the assumption about skipping to the longest day of the year?
            bool inLeafOnPeriod = !this.weather.Sun.IsNorthernHemisphere;
            int leafOnStartDayIndex = -1;
            int leafOnEndDayIndex = -1;
            int longestDayOfYearIndex = -1;
            if (weatherTimeSeries.Timestep == Timestep.Daily)
            {
                for (int dayOfYearIndex = 0, weatherDayIndex = weatherTimeSeries.CurrentYearStartIndex; weatherDayIndex != weatherTimeSeries.NextYearStartIndex; ++dayOfYearIndex, ++weatherDayIndex)
                {
                    if ((longestDayOfYearIndex >= 0) && (dayOfYearIndex < longestDayOfYearIndex))
                    {
                        continue;
                    }

                    float vpdModifier = 1.0F - LeafPhenology<TWeatherTimeSeries>.GetRelativePositionInRange(weatherTimeSeries.VpdMeanInKPa[weatherDayIndex], this.minVpd, this.maxVpd); // high value for low vpd
                    float temperatureModifier = LeafPhenology<TWeatherTimeSeries>.GetRelativePositionInRange(weatherTimeSeries.TemperatureMin[weatherDayIndex], this.minTemp, this.maxTemp);
                    float dayLengthModifier = LeafPhenology<TWeatherTimeSeries>.GetRelativePositionInRange(weather.Sun.GetDayLengthInHours(dayOfYearIndex), this.minDayLength, this.maxDayLength);
                    float leafOnModifier = vpdModifier * temperatureModifier * dayLengthModifier; // Jolly et al. 2005 GSI (growing season index): combined factor of effect of vpd, temperature and day length

                    if ((inLeafOnPeriod == false) && (leafOnModifier > 0.5F))
                    {
                        // switch from leaf off to leaf on
                        inLeafOnPeriod = true;
                        leafOnStartDayIndex = dayOfYearIndex;
                        if (leafOnEndDayIndex != -1)
                        {
                            break; // finished: found both leaf on and leaf off dates
                        }
                        longestDayOfYearIndex = weather.Sun.LongestDayIndex;
                    }
                    else if (inLeafOnPeriod && (leafOnModifier < 0.5))
                    {
                        // switch from leaf on to leaf off
                        leafOnEndDayIndex = dayOfYearIndex;
                        if (leafOnStartDayIndex != -1)
                        {
                            break; // finished: found both leaf on and leaf off dates
                        }
                        longestDayOfYearIndex = weather.Sun.LongestDayIndex;
                        inLeafOnPeriod = false;
                    }
                }

                leafOnStartDayIndex -= 10; // three-week-floating average: subtract 10 days
                leafOnEndDayIndex -= 10;
                if ((leafOnStartDayIndex < -1) || (leafOnEndDayIndex < -1))
                {
                    // throw IException(QString("Phenology::calculation(): was not able to determine the length of the vegetation period for group {0}. weather table: '{1}'.", id(), weather.name()));
                    // Debug.WriteLine("Phenology::calculation(): vegetation period is 0 for group " + LeafType + ", weather table: " + weather.Name);
                    leafOnStartDayIndex = DateTimeExtensions.GetDaysInYear(isLeapYear) - 1; // last day of the year, never reached
                    leafOnEndDayIndex = leafOnStartDayIndex; // never reached
                }
            }
            else if (weatherTimeSeries.Timestep == Timestep.Monthly)
            {
                (int summerSolsticeMonthIndex, int _) = DateTimeExtensions.DayOfYearToDayOfMonth(weather.Sun.LongestDayIndex);
                for (int monthOfYearIndex = 0, weatherMonthIndex = weatherTimeSeries.CurrentYearStartIndex; weatherMonthIndex != weatherTimeSeries.NextYearStartIndex; ++monthOfYearIndex, ++weatherMonthIndex)
                {
                    if ((longestDayOfYearIndex >= 0) && (weatherMonthIndex < summerSolsticeMonthIndex))
                    {
                        continue;
                    }

                    float vpdModifier = 1.0F - LeafPhenology<TWeatherTimeSeries>.GetRelativePositionInRange(weatherTimeSeries.VpdMeanInKPa[weatherMonthIndex], this.minVpd, this.maxVpd); // high value for low vpd
                    float temperatureModifier = LeafPhenology<TWeatherTimeSeries>.GetRelativePositionInRange(weatherTimeSeries.TemperatureMin[weatherMonthIndex], this.minTemp, this.maxTemp);
                    int midMonthIndex = DateTimeExtensions.GetMidmonthDayIndex(monthOfYearIndex, isLeapYear);
                    float dayLengthModifier = LeafPhenology<TWeatherTimeSeries>.GetRelativePositionInRange(weather.Sun.GetDayLengthInHours(midMonthIndex), this.minDayLength, this.maxDayLength);
                    float leafOnModifier = vpdModifier * temperatureModifier * dayLengthModifier; // Jolly et al. 2005 GSI (growing season index): combined factor of effect of vpd, temperature and day length

                    if ((inLeafOnPeriod == false) && (leafOnModifier > 0.5F))
                    {
                        // switch from leaf off to leaf on
                        // TODO: how to interpolate leaf on start day within month?
                        inLeafOnPeriod = true;
                        leafOnStartDayIndex = midMonthIndex;
                        if (leafOnEndDayIndex != -1)
                        {
                            break; // finished: found both leaf on and leaf off dates
                        }
                        longestDayOfYearIndex = weather.Sun.LongestDayIndex;
                    }
                    else if (inLeafOnPeriod && (leafOnModifier < 0.5))
                    {
                        // switch from leaf on to leaf off
                        // TODO: how to interpolate leaf on start day within month?
                        leafOnEndDayIndex = midMonthIndex;
                        if (leafOnStartDayIndex != -1)
                        {
                            break; // finished: found both leaf on and leaf off dates
                        }
                        longestDayOfYearIndex = weather.Sun.LongestDayIndex;
                        inLeafOnPeriod = false;
                    }
                }
            }
            else
            {
                throw new NotSupportedException("Unhandled weather timestep " + weatherTimeSeries.Timestep + ".");
            }

            this.LeafOnStartDayOfYearIndex = leafOnStartDayIndex;
            this.LeafOnEndDayOfYearIndex = leafOnEndDayIndex;

            // set leaf on fractions
            (int leafOnMonthIndex, int leafOnDayOfMonthIndex) = DateTimeExtensions.DayOfYearToDayOfMonth(leafOnStartDayIndex);
            (int leafOffMonthIndex, int leafOffDayOfMonthIndex) = DateTimeExtensions.DayOfYearToDayOfMonth(leafOnEndDayIndex);
            for (int monthIndex = 0; monthIndex < 12; ++monthIndex)
            {
                if ((monthIndex < leafOnMonthIndex) || (monthIndex > leafOffMonthIndex))
                {
                    // whole month is leaf off
                    this.LeafOnFractionByMonth[monthIndex] = 0.0F;
                }
                else if ((monthIndex > leafOnMonthIndex) && (monthIndex < leafOffMonthIndex))
                {
                    // whole month is leaf on
                    this.LeafOnFractionByMonth[monthIndex] = 1.0F;
                }
                else
                {
                    // leaves on or off during this month
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
