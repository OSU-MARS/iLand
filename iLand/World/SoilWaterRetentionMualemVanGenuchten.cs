using iLand.Input;
using System;
using System.Diagnostics;

namespace iLand.World
{
    public class SoilWaterRetentionMualemVanGenuchten : SoilWaterRetention
    {
        private readonly float alphaInKPa;
        private readonly float n;
        private readonly float thetaR;
        private readonly float plantAccessibleWater;

        public SoilWaterRetentionMualemVanGenuchten(ResourceUnitEnvironment environment, float soilSaturationPotentialInKPa)
            : base(environment)
        {
            this.alphaInKPa = environment.SoilVanGenuchtenAlphaInKPa;
            this.n = environment.SoilVanGenuchtenN;
            this.plantAccessibleWater = environment.SoilThetaS - environment.SoilThetaR;
            this.thetaR = environment.SoilThetaR;

            if (Single.IsNaN(soilSaturationPotentialInKPa))
            {
                this.SaturationPotentialInKPa = 0.0F;
            }
            else
            {
                this.SaturationPotentialInKPa = soilSaturationPotentialInKPa;
            }
        }

        public static bool CanCreate(ResourceUnitEnvironment environment)
        {
            return (Single.IsNaN(environment.SoilVanGenuchtenAlphaInKPa) == false) && (Single.IsNaN(environment.SoilVanGenuchtenN) == false) && (Single.IsNaN(environment.SoilThetaR) == false) && (Single.IsNaN(environment.SoilThetaS) == false);
        }

        public override float GetSoilWaterPotentialFromWater(float soilWaterInMM)
        {
            // θ = θr + PAW / (1 + (α |Ψ|)^n)^(1 - 1/n)) -> (1 + (α |Ψ|)^n)^(1 - 1/n) = PAW / (θ - θr) = plantRelativeSaturationInverse, PAW = plantAccessibleWater
            //                                           -> α |Ψ| = ((PAW / (θ - θr))^(1/(1 - 1/n)) - 1)^1/n
            float soilWaterContent = soilWaterInMM / this.SoilPlantAccessibleDepthInMM;
            float plantRelativeSaturationInverse = this.plantAccessibleWater / (soilWaterContent - this.thetaR);
            if (plantRelativeSaturationInverse < 1.0F)
            {
                // check for numerical error as alphaAbsPsi's outer Math.Pow() NaNs if plantRelativeSaturationInverse < 1
                Debug.Assert((plantRelativeSaturationInverse > 0.999999F) && (this.SaturationPotentialInKPa == 0.0F));
                return 0.0F;
            }

            float alphaAbsPsi = MathF.Pow(MathF.Pow(plantRelativeSaturationInverse, 1.0F / (1.0F - 1.0F / this.n)) - 1.0F, 1.0F / this.n);
            float psiInKPa = -alphaAbsPsi / this.alphaInKPa;
            if (psiInKPa > this.SaturationPotentialInKPa)
            {
                // clamp matric potential if numerical error places it slightly above saturation potential
                Debug.Assert(psiInKPa - this.SaturationPotentialInKPa < 1E-6F);
                psiInKPa = this.SaturationPotentialInKPa;
            }

            Debug.Assert((psiInKPa >= -4000.0F) && (psiInKPa <= 0.0F)); // will detect NaN
            return psiInKPa;
        }

        public override float GetSoilWaterFromPotential(float psiInKilopascals)
        {
            float soilWaterContent = this.thetaR + this.plantAccessibleWater / MathF.Pow(1.0F + MathF.Pow(-this.alphaInKPa * psiInKilopascals, this.n), 1.0F - 1.0F / this.n);
            float soilWater = this.SoilPlantAccessibleDepthInMM * soilWaterContent;
            Debug.Assert((psiInKilopascals <= 0.0F) && (soilWaterContent >= this.thetaR) && (soilWaterContent <= this.thetaR + this.plantAccessibleWater));
            return soilWater;
        }
    }
}
