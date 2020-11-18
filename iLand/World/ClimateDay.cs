using System;

namespace iLand.World
{
    // current climate variables of a day. @sa Climate.
    // http://iland.boku.ac.at/ClimateData
    public class ClimateDay
    {
        public int Year { get; set; } // year
        public int Month { get; set; } // month (1..12)
        public int DayOfMonth { get; set; } // day of the month (1..31)
        public float MeanDaytimeTemperature { get; set; } // average day  degree C (of the light hours)
        public float MinTemperature { get; set; } // minimum temperature of the day
        public float MaxTemperature { get; set; } // maximum temperature of the day
        public float TempDelayed { get; set; } // temperature delayed (after Mäkelä 2008) for response calculations
        public float Preciptitation { get; set; } // sum of day [mm]
        public float Radiation { get; set; } // sum of day (MJ/m2)
        public float Vpd { get; set; } // average of day [kPa] = [0.1 mbar] (1 bar = 100kPa)

        //public int ID() { return Year * 10000 + Month * 100 + DayOfMonth; }
        public float MeanTemperature() { return 0.5F * (this.MinTemperature + MaxTemperature); } // mean temperature

        public override string ToString()
        {
            return String.Concat(this.Year + "-" + this.Month + "-" + this.DayOfMonth);
        }
    }
}
