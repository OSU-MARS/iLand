using iLand.Input;
using System;
using System.Diagnostics;

namespace iLand.World
{
    internal class SoilWaterRetentionMualemVanGenuchten : SoilWaterRetention
    {
        private readonly float alpha;
        private readonly float n;
        private readonly float soilPlantAccessibleDepthInMM;
        private readonly float thetaR;
        private readonly float plantAccessibleWater;

        public SoilWaterRetentionMualemVanGenuchten(ResourceUnitEnvironment environment)
        {
            this.alpha = environment.SoilVanGenuchtenAlphaInKPa;
            this.n = environment.SoilVanGenuchtenN;
            this.plantAccessibleWater = environment.SoilThetaS - environment.SoilThetaR;
            this.soilPlantAccessibleDepthInMM = 10.0F * environment.SoilPlantAccessibleDepthInCm;
            this.thetaR = environment.SoilThetaR;

            this.SaturationPotentialInKPa = 0.0F;
        }

        public static bool CanCreate(ResourceUnitEnvironment environment)
        {
            return (Single.IsNaN(environment.SoilVanGenuchtenAlphaInKPa) == false) && (Single.IsNaN(environment.SoilVanGenuchtenN) == false) && (Single.IsNaN(environment.SoilThetaR) == false) && (Single.IsNaN(environment.SoilThetaS) == false);
        }

        public override float GetSoilWaterPotentialFromWater(float soilWaterInMM)
        {
            float soilWaterContent = soilWaterInMM / this.soilPlantAccessibleDepthInMM;
            float plantRelativeSaturation = this.plantAccessibleWater / (soilWaterContent - this.thetaR);
            float alphaAbsPsi = MathF.Pow(MathF.Pow(plantRelativeSaturation, 1.0F / (1.0F - 1.0F / this.n)), 1.0F / this.n) - 1.0F;
            float psiInKPa = -alphaAbsPsi / this.alpha;
            if (psiInKPa > this.SaturationPotentialInKPa)
            {
                // clamp matric potential if numerical error places it slightly above saturation potential
                Debug.Assert(psiInKPa - this.SaturationPotentialInKPa < 1E-6F);
                psiInKPa = this.SaturationPotentialInKPa;
            }
            return psiInKPa;
        }

        public override float GetSoilWaterFromPotential(float psiInKilopascals)
        {
            float soilWaterContent = this.thetaR + this.plantAccessibleWater / MathF.Pow(1.0F + MathF.Pow(-this.alpha * psiInKilopascals, this.n), 1.0F - 1.0F / this.n);
            float soilWater = this.soilPlantAccessibleDepthInMM * soilWaterContent;
            Debug.Assert((psiInKilopascals <= 0.0F) && (soilWaterContent >= this.thetaR) && (soilWaterContent <= this.thetaR + this.plantAccessibleWater));
            return soilWater;
        }
    }
}
