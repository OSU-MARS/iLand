﻿namespace iLand.Tree
{
    public class EstablishmentParameters
    {
        public int ChillRequirement { get; set; } // days of chilling requirement
        public float FrostTolerance { get; set; } //factor in growing season frost tolerance calculation
        public float GddBaseTemperature { get; set; } // for GDD-calc: GDD=sum(T - baseTemp)
        public int GddBudBurst { get; set; } // GDDs needed until bud burst
        public int GddMin { get; set; } // GDD thresholds
        public int GddMax { get; set; }
        public int MinFrostFree { get; set; } // minimum number of annual frost-free days required
        public float MinTemp { get; set; } //degC
        public float PsiMin { get; set; } // minimum soil water potential for establishment

        public EstablishmentParameters()
        {
            this.ChillRequirement = 56;
            this.FrostTolerance = 0.5F;
            this.GddBaseTemperature = 3.4F;
            this.GddBudBurst = 255;
            this.GddMin = 177;
            this.GddMax = 3261;
            this.MinFrostFree = 65;
            this.MinTemp = -37.0F;
            this.PsiMin = 0.0F;
        }
    }
}
