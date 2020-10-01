namespace iLand.Core
{
    public class EstablishmentParameters
    {
        public double MinTemp { get; set; } //degC
        public int ChillRequirement { get; set; } // days of chilling requirement
        public int GddMin { get; set; } // GDD thresholds
        public int GddMax { get; set; }
        public double GddBaseTemperature { get; set; } // for GDD-calc: GDD=sum(T - baseTemp)
        public int GddBudBurst { get; set; } // GDDs needed until bud burst
        public int MinFrostFree { get; set; } // minimum number of annual frost-free days required
        public double FrostTolerance { get; set; } //factor in growing season frost tolerance calculation
        public double PsiMin { get; set; } // minimum soil water potential for establishment

        public EstablishmentParameters()
        {
            MinTemp = -37;
            ChillRequirement = 56;
            GddMin = 177;
            GddMax = 3261;
            GddBaseTemperature = 3.4;
            GddBudBurst = 255;
            MinFrostFree = 65;
            FrostTolerance = 0.5;
            PsiMin = 0.0;
        }
    }
}
