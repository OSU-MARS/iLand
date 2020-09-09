namespace iLand.core
{
    internal class EstablishmentParameters
    {
        public double min_temp; //degC
        public int chill_requirement; // days of chilling requirement
        public int GDD_min, GDD_max; // GDD thresholds
        public double GDD_baseTemperature; // for GDD-calc: GDD=sum(T - baseTemp)
        public int bud_birst; // GDDs needed until bud burst
        public int frost_free; // minimum number of annual frost-free days required
        public double frost_tolerance; //factor in growing season frost tolerance calculation
        public double psi_min; // minimum soil water potential for establishment

        public EstablishmentParameters()
        {
            min_temp = -37;
            chill_requirement = 56;
            GDD_min = 177;
            GDD_max = 3261;
            GDD_baseTemperature = 3.4;
            bud_birst = 255;
            frost_free = 65;
            frost_tolerance = 0.5;
            psi_min = 0.0;
        }
    }
}
