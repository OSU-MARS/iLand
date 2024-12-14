// C++/core/species.h
namespace iLand.Tree
{
    public class SaplingEstablishmentParameters // C++: EstablishmentParameters
    {
        public int ChillingDaysRequired { get; set; } // days of chilling requirement, C++ chill_requirement
        public float ColdFatalityTemperature { get; set; } // cold fatality temperature, °C
        public float DroughtMortalityPsiInMPa { get; set; } // minimum soil water potential for establishment, C++ psi_min
        public float FrostTolerance { get; set; } // factor in growing season frost tolerance calculation, C++ frost_tolerance
        public float GrowingDegreeDaysBaseTemperature { get; set; } // for GDD-calc: GDD=sum(T - baseTemp)
        public int GrowingDegreeDaysForBudburst { get; set; } // GDDs needed until bud burst, C++ bud_birst [sic]
        public int MinimumGrowingDegreeDays { get; set; } // GDD thresholds, C++ GDD_min
        public int MaximumGrowingDegreeDays { get; set; } // C++ GDD_max
        public int MinimumFrostFreeDays { get; set; } // minimum number of annual frost-free days required, C++ frost_free
        public float SOL_thickness { get; set; } ///< effect of thick soil organic layer (0: no effect), C++ SOL_thickness

        public SaplingEstablishmentParameters()
        {
            this.ChillingDaysRequired = 56;
            this.ColdFatalityTemperature = -37.0F;
            this.DroughtMortalityPsiInMPa = 0.0F;
            this.FrostTolerance = 0.5F;
            this.GrowingDegreeDaysBaseTemperature = 3.4F;
            this.GrowingDegreeDaysForBudburst = 255;
            this.MinimumGrowingDegreeDays = 177;
            this.MaximumGrowingDegreeDays = 3261;
            this.MinimumFrostFreeDays = 65;
            this.SOL_thickness = 0.0F;
        }
    }
}
