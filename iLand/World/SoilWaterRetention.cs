using iLand.Input;
using System;

namespace iLand.World
{
    internal abstract class SoilWaterRetention
    {
        public float SaturationPotentialInKPa { get; protected init; }

        protected SoilWaterRetention()
        {
            this.SaturationPotentialInKPa = Single.NaN;
        }

        public static SoilWaterRetention Create(ResourceUnitEnvironment environment)
        {
            if (Single.IsNaN(environment.SoilPlantAccessibleDepthInCm) || (environment.SoilPlantAccessibleDepthInCm < 0.0F))
            {
                throw new ArgumentOutOfRangeException(nameof(environment));
            }

            if (SoilWaterRetentionMualemVanGenuchten.CanCreate(environment))
            {
                return new SoilWaterRetentionMualemVanGenuchten(environment);
            }
            if (SoilWaterRetentionCampbell.CanCreate(environment))
            {
                return new SoilWaterRetentionCampbell(environment);
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
    }
}
