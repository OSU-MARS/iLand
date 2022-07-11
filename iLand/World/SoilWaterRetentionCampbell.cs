using iLand.Input;
using System;
using System.Diagnostics;

namespace iLand.World
{
    internal class SoilWaterRetentionCampbell : SoilWaterRetention
    {
        private readonly float saturationRatioPowerB; // see GetSoilWater*()
        private readonly float soilDepthInMM;
        private readonly float saturatedSoilWaterContent; // see GetSoilWater*(), [-], m3/m3

        public SoilWaterRetentionCampbell(ResourceUnitEnvironment environment)
        {
            this.soilDepthInMM = 10.0F * environment.SoilDepthInCm; // convert from cm to mm TODO: zero is not a realistic default

            // get values...
            float percentSand = environment.SoilSand;
            float percentSilt = environment.SoilSilt;
            float percentClay = environment.SoilClay;
            if (Math.Abs(100.0 - (percentSand + percentSilt + percentClay)) > 0.01)
            {
                throw new NotSupportedException("Soil textures do not sum to 100% within 0.01% for resource unit " + environment.ResourceUnitID + ". Sand: " + percentSand + "%, silt: " + percentSilt + "%, clay: " + percentClay + "%.");
            }

            // calculate soil characteristics based on empirical functions from sparse United States data: 35 points in 23 states
            // Schwalm CR, Ek AR. 2004. A process-based model of forest ecosystems driven by meteorology. Ecological Modelling 179(3):317-348.
            //   https://doi.org/10.1016/j.ecolmodel.2004.04.016
            // Cosby BJ, Hornberger GM, Clapp RB, Ginn TR. 1984. A Statistical Exploration of the Relationships of Soil Moisture Characteristics
            //   to the Physical Properties of Soils. Water Resources Research 20(6): 682-690. https://doi.org/10.1029/WR020i006p00682
            // Variables are percentages [0..100] with Schwalm and Ek reusing Cosby et al.'s simple linear regressions. Cosby et al. regressed
            // on two earlier studies sampling 35 points in 23 states, primarily in the eastern United States and preferentially in the southeastern
            // United States.
            // TODO: Update this to some pedotransfer function with a broader calibration. See 
            // A Global High-Resolution Data Set of Soil Hydraulic and Thermal Properties for Land Surface Modeling
            // Dai Y, Xin Q, Wei N, et al. 2019. A Global High-Resolution Data Set of Soil Hydraulic and Thermal Properties for Land Surface
            //   Modeling. Journal of Advances in Modeling Earth Systems 11(9):2996-3023. https://doi.org/10.1029/2019MS001784
            // Zhang X, Zhu J, Wendroth O, et al. 2019. Effect of Macroporosity on Pedotransfer Function Estimates at the Field Scale. Vadose Zone
            //   Journal 18(1):1-15. https://doi.org/10.2136/vzj2018.08.0151
            // Zhang Y, Schaap G. 2017. Weighted recalibration of the Rosetta pedotransfer model with improved estimates of hydraulic parameter
            //   distributions and summary statistics (Rosetta3). Journal of Hydrology 547:39-53. https://doi.org/10.1016/j.jhydrol.2017.01.004
            this.SaturationPotentialInKPa = -0.000098F * MathF.Exp(2.30258509299F * (1.54F - 0.0095F * percentSand + 0.0063F * percentSilt)); // Schwalm and Ek eq. 83, Cosby et al. Table 4, Campbell and Norman 1998, ln(10) = 2.30258509299
            this.saturationRatioPowerB = -(3.1F + 0.157F * percentClay - 0.003F * percentSand);  // eq. 84
            this.saturatedSoilWaterContent = 0.01F * (50.5F - 0.142F * percentSand - 0.037F * percentClay); // eq. 78
        }

        public static bool CanCreate(ResourceUnitEnvironment environment)
        {
            return (Single.IsNaN(environment.SoilSand) == false) && (Single.IsNaN(environment.SoilSilt) == false) && (Single.IsNaN(environment.SoilClay) == false);
        }

        public override float GetSoilWaterPotentialFromWater(float soilWaterInMM)
        {
            Debug.Assert((soilWaterInMM >= 0.0F) && (this.soilDepthInMM > soilWaterInMM), "Soil depth is negative, soil water content is negative, or soil water content exceeds soil depth.");

            // psi_x = psi_ref * (θ / θref)^b
            if (soilWaterInMM < 0.001F)
            {
                return -100000000.0F;
            }
            float psiInKPa = this.SaturationPotentialInKPa * MathF.Pow(soilWaterInMM / this.soilDepthInMM / this.saturatedSoilWaterContent, this.saturationRatioPowerB);
            return psiInKPa;
        }

        public override float GetSoilWaterFromPotential(float psiInKilopascals)
        {
            Debug.Assert(psiInKilopascals <= 0.0F, "Soil depth is negative or matric potential is positive. Are the arguments reversed?");

            // rho_x = rho_ref * (psi_x / psi_ref)^(1/b)
            float mmH20 = this.soilDepthInMM * this.saturatedSoilWaterContent * MathF.Pow(psiInKilopascals / this.SaturationPotentialInKPa, 1.0F / this.saturationRatioPowerB);
            return mmH20;
        }
    }
}
