namespace iLand.core
{
    // current climate variables of a day. @sa Climate.
    // http://iland.boku.ac.at/ClimateData
    internal class ClimateDay
    {
        public static double co2 = 350.0; // ambient CO2 content in ppm

        public int year; // year
        public int month; // month (1..12)
        public int dayOfMonth; // day of the month (1..31)
        public double temperature; // average day  degree C (of the light hours)
        public double min_temperature; // minimum temperature of the day
        public double max_temperature; // maximum temperature of the day
        public double temp_delayed; // temperature delayed (after Maekela, 2008) for response calculations
        public double preciptitation; // sum of day [mm]
        public double radiation; // sum of day (MJ/m2)
        public double vpd; // average of day [kPa] = [0.1 mbar] (1 bar = 100kPa)

        public int id() { return year * 10000 + month * 100 + dayOfMonth; }
        public bool isValid() { return (year>=0); }
        public double mean_temp() { return (min_temperature + max_temperature) / 2.0; } // mean temperature

        public string toString()
        {
            return string.Concat(dayOfMonth + "." + month + "." + year);
        }
    }
}
