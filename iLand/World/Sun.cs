// C++/core/climate.h
using iLand.Tool;
using System;

namespace iLand.World
{
    public class Sun
    {
        private static readonly float EarthAxialTilt = Maths.ToRadians(23.439F); // angle of Earth's axial tilt (obliquity) with respect to ecliptic

        private readonly float latitudeInRadians; // latitude in radians
        private readonly float[] dayLengthInHours; // daylength per day in hours

        public int LastDayLongerThan10_5Hours { get; private set; } // index of last day of year with a day length > 10.5 hours (see Establishment)
        public int LastDayLongerThan14_5Hours { get; private set; } // index of last day with at least 14.5 hours of day length
        public int LongestDayIndex { get; private set; } // day of year with maximum day length

        public Sun(float latitudeInDegrees)
        {
            if ((latitudeInDegrees < -90.0F) || (latitudeInDegrees > 90.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(latitudeInDegrees), "Latitude is beyond 90° north or south.");
            }

            this.dayLengthInHours = new float[Constant.Time.DaysInLeapYear];
            this.latitudeInRadians = Maths.ToRadians(latitudeInDegrees);

            // calculate length of day using the approximation formulae of
            //   Glarner H. ND. Length of Day and Twilight. http://herbert.gandraxa.com/length_of_day.aspx
            //   Weins T. 2015. Day Length. https://www.mathworks.com/matlabcentral/fileexchange/20390-day-length (MATLAB implementation of Glarner)
            // TODO: support more accurate calcuation of summer and winter solstices?
            // TODO: support leap years by adding 0.25 for each year in within four year leap year cycle (see Glarner)
            if (this.latitudeInRadians > 0.0F)
            {
                this.LongestDayIndex = 182 - 10; // approximate northern hemisphere summer solstice as June 21
            }
            else
            {
                this.LongestDayIndex = 365 - 10; // approximate sourthern hemisphere summer solstice as December 21
            }

            for (int dayOfCalendarYearIndex = 0; dayOfCalendarYearIndex < this.dayLengthInHours.Length; ++dayOfCalendarYearIndex)
            {
                // day 0 of solar year is winter solstice => add 10 days to calendar year index to get solar year index
                int dayOfSolarYearIndex = dayOfCalendarYearIndex + 10;
                float m = 1.0F - MathF.Tan(latitudeInRadians) * MathF.Tan(Sun.EarthAxialTilt * MathF.Cos(MathF.PI / 182.625F * dayOfSolarYearIndex)); // 182.625 = 0.5 * 365.25
                m = Maths.Limit(m, 0.0F, 2.0F);
                this.dayLengthInHours[dayOfCalendarYearIndex] = 24.0F / MathF.PI * MathF.Acos(1.0F - m); // result in hours [0..24]
            }
            this.LastDayLongerThan10_5Hours = 0;
            for (int day = this.LongestDayIndex; day < this.dayLengthInHours.Length; day++)
            {
                if (this.dayLengthInHours[day] < 10.5F)
                {
                    this.LastDayLongerThan10_5Hours = day;
                    break;
                }
            }
            this.LastDayLongerThan14_5Hours = 0;
            for (int day = this.LongestDayIndex; day < this.dayLengthInHours.Length; day++)
            {
                if (dayLengthInHours[day] < 14.5F)
                {
                    this.LastDayLongerThan14_5Hours = day;
                    break;
                }
            }
        }

        public bool IsNorthernHemisphere
        {
            get { return this.latitudeInRadians > 0.0F; }
        }

        public float GetDayLengthInHours(int dayOfYearIndex) 
        { 
            return this.dayLengthInHours[dayOfYearIndex]; 
        }
    }
}
