namespace iLand.Tree
{
    public class SaplingEstablishmentParameters
    {
        public int ChillRequirement { get; set; } // days of chilling requirement
        public float ColdFatalityTemperature { get; set; } // cold fatality temperature, °C
        public float DroughtMortalityPsiInMPa { get; set; } // minimum soil water potential for establishment
        public float FrostTolerance { get; set; } // factor in growing season frost tolerance calculation
        public float GrowingDegreeDaysBaseTemperature { get; set; } // for GDD-calc: GDD=sum(T - baseTemp)
        public int GrowingDegreeDaysBudBurst { get; set; } // GDDs needed until bud burst
        public int MinimumGrowingDegreeDays { get; set; } // GDD thresholds
        public int MaximumGrowingDegreeDays { get; set; }
        public int MinimumFrostFreeDays { get; set; } // minimum number of annual frost-free days required

        public SaplingEstablishmentParameters()
        {
            this.ChillRequirement = 56;
            this.ColdFatalityTemperature = -37.0F;
            this.DroughtMortalityPsiInMPa = 0.0F;
            this.FrostTolerance = 0.5F;
            this.GrowingDegreeDaysBaseTemperature = 3.4F;
            this.GrowingDegreeDaysBudBurst = 255;
            this.MinimumGrowingDegreeDays = 177;
            this.MaximumGrowingDegreeDays = 3261;
            this.MinimumFrostFreeDays = 65;
        }
    }
}
