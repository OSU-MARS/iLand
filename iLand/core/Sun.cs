using System;
using System.Text;

namespace iLand.core
{
    internal class Sun
    {
        private const double j = Math.PI / 182.625;
        private static readonly double ecliptic = Global.RAD(23.439);

        private double mLatitude; ///< latitude in radians
        private int mDayWithMaxLength; ///< day of year with maximum day length
        private double[] mDaylength_h; ///< daylength per day in hours
        private int mDayWith10_5hrs; // last day of year with a day length > 10.5 hours (see Establishment)
        private int mDayWith14_5hrs; // last doy with at least 14.5 hours of day length

        public Sun()
        {
            this.mDaylength_h = new double[366];
        }

        public double daylength(int day) { return mDaylength_h[day]; }
        public int dayShorter10_5hrs() { return mDayWith10_5hrs; }
        public int dayShorter14_5hrs() { return mDayWith14_5hrs; }
        public int longestDay() { return mDayWithMaxLength; }
        public bool northernHemishere() { return mDayWithMaxLength < 300; }

        public string dump()
        {
            StringBuilder result = new StringBuilder();
            result.AppendLine(String.Format("lat: {0}, longest day: {1}", mLatitude, mDayWithMaxLength));
            result.AppendLine("day;daylength");
            for (int day = 0; day < 366; ++day)
            {
                result.AppendLine(String.Format("{0};{1}", day, mDaylength_h[day]));
            }
            return result.ToString();
        }

        public void setup(double latitude_rad)
        {
            mLatitude = latitude_rad;
            if (mLatitude > 0)
                mDayWithMaxLength = 182 - 10; // 21.juni
            else
                mDayWithMaxLength = 365 - 10; //southern hemisphere
                                              // calculate length of day using  the approximation formulae of: http://herbert.gandraxa.com/length_of_day.aspx
            double m;
            for (int day = 0; day < 366; day++)
            {
                m = 1.0 - Math.Tan(latitude_rad) * Math.Tan(ecliptic * Math.Cos(j * (day + 10))); // day=0: winter solstice => subtract 10 days
                m = Global.limit(m, 0.0, 2.0);
                mDaylength_h[day] = Math.Acos(1 - m) / Math.PI * 24.0; // result in hours [0..24]
            }
            mDayWith10_5hrs = 0;
            for (int day = mDayWithMaxLength; day < 366; day++)
            {
                if (mDaylength_h[day] < 10.5)
                {
                    mDayWith10_5hrs = day;
                    break;
                }
            }
            mDayWith14_5hrs = 0;
            for (int day = mDayWithMaxLength; day < 366; day++)
            {
                if (mDaylength_h[day] < 14.5)
                {
                    mDayWith14_5hrs = day;
                    break;
                }
            }
        }
    }
}
