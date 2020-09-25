using System;

namespace iLand.core
{
    // current climate variables of a day. @sa Climate.
    // http://iland.boku.ac.at/ClimateData
    internal class ClimateDay
    {
        public static double CarbonDioxidePpm = 350.0; // ambient CO2 content in ppm

        public int Year { get; set; } // year
        public int Month { get; set; } // month (1..12)
        public int DayOfMonth { get; set; } // day of the month (1..31)
        public double MeanDaytimeTemperature { get; set; } // average day  degree C (of the light hours)
        public double MinTemperature { get; set; } // minimum temperature of the day
        public double MaxTemperature { get; set; } // maximum temperature of the day
        public double TempDelayed { get; set; } // temperature delayed (after Maekela, 2008) for response calculations
        public double Preciptitation { get; set; } // sum of day [mm]
        public double Radiation { get; set; } // sum of day (MJ/m2)
        public double Vpd { get; set; } // average of day [kPa] = [0.1 mbar] (1 bar = 100kPa)

        public int ID() { return Year * 10000 + Month * 100 + DayOfMonth; }
        public bool IsValid() { return Year >= 0; }
        public double MeanTemperature() { return (MinTemperature + MaxTemperature) / 2.0; } // mean temperature

        public override string ToString()
        {
            return String.Concat(DayOfMonth + "." + Month + "." + Year);
        }
    }
}
