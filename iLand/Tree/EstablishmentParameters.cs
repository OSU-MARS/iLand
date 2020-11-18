namespace iLand.Tree
{
    public class EstablishmentParameters
    {
        public int ChillRequirement { get; set; } // days of chilling requirement
        public float FrostTolerance { get; set; } // factor in growing season frost tolerance calculation
        public float GrowingDegreeDaysBaseTemperature { get; set; } // for GDD-calc: GDD=sum(T - baseTemp)
        public int GddBudBurst { get; set; } // GDDs needed until bud burst
        public int MinimumGrowingDegreeDays { get; set; } // GDD thresholds
        public int MaximumGrowingDegreeDays { get; set; }
        public int MinimumFrostFreeDays { get; set; } // minimum number of annual frost-free days required
        public float MinTemp { get; set; } // cold fatality temperature, °C
        public float PsiMin { get; set; } // minimum soil water potential for establishment

        public EstablishmentParameters()
        {
            this.ChillRequirement = 56;
            this.FrostTolerance = 0.5F;
            this.GrowingDegreeDaysBaseTemperature = 3.4F;
            this.GddBudBurst = 255;
            this.MinimumGrowingDegreeDays = 177;
            this.MaximumGrowingDegreeDays = 3261;
            this.MinimumFrostFreeDays = 65;
            this.MinTemp = -37.0F;
            this.PsiMin = 0.0F;
        }
    }
}
