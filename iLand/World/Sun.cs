using iLand.Tool;
using System;
using System.Text;

namespace iLand.World
{
    public class Sun
    {
        private const float J = MathF.PI / 182.625F;
        private static readonly float Ecliptic = Maths.ToRadians(23.439F);

        private float mLatitudeInRadians; // latitude in radians
        private readonly float[] mDayLengthInHours; // daylength per day in hours

        public int LastDayLongerThan10_5Hours { get; private set; } // last day of year with a day length > 10.5 hours (see Establishment)
        public int LastDayLongerThan14_5Hours { get; private set; } // last day with at least 14.5 hours of day length
        public int LongestDay { get; private set; } // day of year with maximum day length

        public Sun()
        {
            this.mDayLengthInHours = new float[Constant.DaysInLeapYear];
        }

        public float GetDayLengthInHours(int dayOfYear) { return this.mDayLengthInHours[dayOfYear]; }
        public bool IsNorthernHemisphere() { return this.LongestDay < 300; }

        public string Dump()
        {
            StringBuilder result = new();
            result.AppendLine(String.Format("lat: {0}, longest day: {1}", mLatitudeInRadians, LongestDay));
            result.AppendLine("day;daylength");
            for (int day = 0; day < this.mDayLengthInHours.Length; ++day)
            {
                result.AppendLine(String.Format("{0};{1}", day, mDayLengthInHours[day]));
            }
            return result.ToString();
        }

        public void Setup(float latitudeInRadians)
        {
            if ((latitudeInRadians < -Constant.HalfPi) || (latitudeInRadians > Constant.HalfPi))
            {
                throw new ArgumentOutOfRangeException(nameof(latitudeInRadians), "Latitude is beyond 90° north or south.");
            }

            this.mLatitudeInRadians = latitudeInRadians;
            if (this.mLatitudeInRadians > 0.0F)
            {
                this.LongestDay = 182 - 10; // 21.juni
            }
            else
            {
                this.LongestDay = 365 - 10; //southern hemisphere
            }
             
            // calculate length of day using the approximation formulae of: http://herbert.gandraxa.com/length_of_day.aspx
            for (int dayOfYear = 0; dayOfYear < this.mDayLengthInHours.Length; ++dayOfYear)
            {
                float m = 1.0F - MathF.Tan(latitudeInRadians) * MathF.Tan(Sun.Ecliptic * MathF.Cos(Sun.J * (dayOfYear + 10))); // day=0: winter solstice => subtract 10 days
                m = Maths.Limit(m, 0.0F, 2.0F);
                mDayLengthInHours[dayOfYear] = 24.0F / MathF.PI * MathF.Acos(1.0F - m); // result in hours [0..24]
            }
            this.LastDayLongerThan10_5Hours = 0;
            for (int day = LongestDay; day < this.mDayLengthInHours.Length; day++)
            {
                if (mDayLengthInHours[day] < 10.5)
                {
                    this.LastDayLongerThan10_5Hours = day;
                    break;
                }
            }
            this.LastDayLongerThan14_5Hours = 0;
            for (int day = LongestDay; day < this.mDayLengthInHours.Length; day++)
            {
                if (mDayLengthInHours[day] < 14.5)
                {
                    this.LastDayLongerThan14_5Hours = day;
                    break;
                }
            }
        }
    }
}
