using iLand.Input;
using System;

namespace iLand.World
{
    public abstract class SoilWaterRetention
    {
        public float SaturationPotentialInKPa { get; protected init; }
        public float SoilPlantAccessibleDepthInMM { get; protected set; }

        protected SoilWaterRetention(ResourceUnitEnvironment environment)
        {
            this.SaturationPotentialInKPa = Single.NaN;
            this.SoilPlantAccessibleDepthInMM = 10.0F * environment.SoilPlantAccessibleDepthInCm; // convert from cm to mm
        }

        public static SoilWaterRetention Create(ResourceUnitEnvironment environment, float soilSaturationPotentialInKPa)
        {
            if (Single.IsNaN(environment.SoilPlantAccessibleDepthInCm) || (environment.SoilPlantAccessibleDepthInCm < 0.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(environment));
            }

            if (SoilWaterRetentionMualemVanGenuchten.CanCreate(environment))
            {
                return new SoilWaterRetentionMualemVanGenuchten(environment, soilSaturationPotentialInKPa);
            }
            if (SoilWaterRetentionCampbell.CanCreate(environment))
            {
                return new SoilWaterRetentionCampbell(environment, soilSaturationPotentialInKPa);
            }

            throw new NotSupportedException("Unable to create soil water retention curve for resource unit " + environment.ResourceUnitID + ".");
        }

        /// <summary>
        /// calculate the water pressure [saugspannung] for a given amount of water
        /// </summary>
        /// <param name="soilWaterInMM"></param>
        /// <returns>water potential in kPa</returns>
        /// <remarks>https://iland-model.org/water+cycle#soil_water_pool</remarks>
        public abstract float GetSoilWaterPotentialFromWater(float soilWaterInMM);

        /// <summary>
        /// calculate the height of the water column for a given pressure
        /// </summary>
        /// <param name="psiInKilopascals"></param>
        /// <returns>water amount in mm</returns>
        /// <remarks>https://iland-model.org/water+cycle#soil_water_pool</remarks>
        public abstract float GetSoilWaterFromPotential(float psiInKilopascals);

        // can be made virtual if needed
        // SoilWaterRetentionMualemVanGenuchten and SoilWaterRetentionCampbell don't need to override.
        public void SetActiveLayerDepth(float activeLayerDepthInMM)
        {
            this.SoilPlantAccessibleDepthInMM = activeLayerDepthInMM;
        }
    }
}
