using iLand.Tools;
using System;
using System.Text;

namespace iLand.World
{
    public class Sun
    {
        private const double J = Math.PI / 182.625;
        private static readonly double Ecliptic = Global.ToRadians(23.439);

        private double mLatitudeInRadians; // latitude in radians
        private readonly double[] mDayLengthInHours; // daylength per day in hours

        public int LastDayLongerThan10_5Hours { get; private set; } // last day of year with a day length > 10.5 hours (see Establishment)
        public int LastDayLongerThan14_5Hours { get; private set; } // last day with at least 14.5 hours of day length
        public int LongestDay { get; private set; } // day of year with maximum day length

        public Sun()
        {
            this.mDayLengthInHours = new double[Constant.DaysInLeapYear];
        }

        public double GetDaylength(int day) { return mDayLengthInHours[day]; }
        public bool NorthernHemisphere() { return LongestDay < 300; }

        public string Dump()
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(String.Format("lat: {0}, longest day: {1}", mLatitudeInRadians, LongestDay));
            result.AppendLine("day;daylength");
            for (int day = 0; day < this.mDayLengthInHours.Length; ++day)
            {
                result.AppendLine(String.Format("{0};{1}", day, mDayLengthInHours[day]));
            }
            return result.ToString();
        }

        public void Setup(double latitudeInRadians)
        {
            mLatitudeInRadians = latitudeInRadians;
            if (mLatitudeInRadians > 0)
            {
                LongestDay = 182 - 10; // 21.juni
            }
            else
            {
                LongestDay = 365 - 10; //southern hemisphere
            }
             
            // calculate length of day using  the approximation formulae of: http://herbert.gandraxa.com/length_of_day.aspx
            double m;
            for (int day = 0; day < this.mDayLengthInHours.Length; day++)
            {
                m = 1.0 - Math.Tan(latitudeInRadians) * Math.Tan(Ecliptic * Math.Cos(J * (day + 10))); // day=0: winter solstice => subtract 10 days
                m = Global.Limit(m, 0.0, 2.0);
                mDayLengthInHours[day] = Math.Acos(1 - m) / Math.PI * 24.0; // result in hours [0..24]
            }
            LastDayLongerThan10_5Hours = 0;
            for (int day = LongestDay; day < this.mDayLengthInHours.Length; day++)
            {
                if (mDayLengthInHours[day] < 10.5)
                {
                    LastDayLongerThan10_5Hours = day;
                    break;
                }
            }
            LastDayLongerThan14_5Hours = 0;
            for (int day = LongestDay; day < this.mDayLengthInHours.Length; day++)
            {
                if (mDayLengthInHours[day] < 14.5)
                {
                    LastDayLongerThan14_5Hours = day;
                    break;
                }
            }
        }
    }
}
