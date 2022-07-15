using iLand.Tool;
using System;

namespace iLand.World
{
    public class Sun
    {
        private const float J = MathF.PI / 182.625F;
        private static readonly float Ecliptic = Maths.ToRadians(23.439F);

        private readonly float latitudeInRadians; // latitude in radians
        private readonly float[] dayLengthInHours; // daylength per day in hours

        public int LastDayLongerThan10_5Hours { get; private set; } // last day of year with a day length > 10.5 hours (see Establishment)
        public int LastDayLongerThan14_5Hours { get; private set; } // last day with at least 14.5 hours of day length
        public int LongestDayIndex { get; private set; } // day of year with maximum day length

        public Sun(float latitudeInDegrees)
        {
            if ((latitudeInDegrees < -90.0F) || (latitudeInDegrees > 90.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(latitudeInDegrees), "Latitude is beyond 90° north or south.");
            }

            this.dayLengthInHours = new float[Constant.DaysInLeapYear];

            this.latitudeInRadians = Maths.ToRadians(latitudeInDegrees);
            // TODO: support more accurate calcuation of summer or winter soilstice?
            if (this.latitudeInRadians > 0.0F)
            {
                this.LongestDayIndex = 182 - 10; // June 21, non-leap years
            }
            else
            {
                this.LongestDayIndex = 365 - 10; // southern hemisphere, non-leap years
            }

            // calculate length of day using the approximation formulae of: http://herbert.gandraxa.com/length_of_day.aspx
            for (int dayOfYear = 0; dayOfYear < this.dayLengthInHours.Length; ++dayOfYear)
            {
                float m = 1.0F - MathF.Tan(latitudeInRadians) * MathF.Tan(Sun.Ecliptic * MathF.Cos(Sun.J * (dayOfYear + 10))); // day=0: winter solstice => subtract 10 days
                m = Maths.Limit(m, 0.0F, 2.0F);
                dayLengthInHours[dayOfYear] = 24.0F / MathF.PI * MathF.Acos(1.0F - m); // result in hours [0..24]
            }
            this.LastDayLongerThan10_5Hours = 0;
            for (int day = this.LongestDayIndex; day < this.dayLengthInHours.Length; day++)
            {
                if (dayLengthInHours[day] < 10.5F)
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
