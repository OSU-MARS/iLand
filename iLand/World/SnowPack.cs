using System;

namespace iLand.World
{
    internal class SnowPack
    {
        public float MeltTemperatureInC { get; set; } // Threshold temperature for snowing / snow melt
        public float WaterEquivalentInMM { get; set; } // height of snowpack (mm water column)

        public SnowPack()
        {
            this.MeltTemperatureInC = 0.0F;
            this.WaterEquivalentInMM = 0.0F;
        }

        /// additional precipitation (e.g. non evaporated water of canopy interception).
        public float AddSnowWaterEquivalent(float preciptitationInMM, float temperatureC)
        {
            // no snow to add if temp >0 C
            if (temperatureC > this.MeltTemperatureInC)
            {
                return preciptitationInMM;
            }

            // temps < 0 C: add to snow
            this.WaterEquivalentInMM += preciptitationInMM;
            return 0.0F;
        }

        /// process the snow layer. Returns the mm of preciptitation/melt water that leaves the snow layer.
        /** calculates the input/output of water that is stored in the snow pack.
            The approach is similar to Picus 1.3 and ForestBGC (Running, 1988). Assumes zero sublimation.
            Returns the amount of water that exits the snowpack (precipitation, snow melt) */
        public float FlowDay(float preciptitationInMM, float temperatureC)
        {
            if (temperatureC > this.MeltTemperatureInC)
            {
                if (this.WaterEquivalentInMM == 0.0F)
                {
                    return preciptitationInMM; // no change
                }

                // melting from rain on snow event
                const float meltingCoefficient = 0.7F; // mm/C
                float snowmelt = MathF.Min((temperatureC - this.MeltTemperatureInC) * meltingCoefficient, this.WaterEquivalentInMM);
                this.WaterEquivalentInMM -= snowmelt;
                return preciptitationInMM + snowmelt;
            }

            // assume all precipitation on days below melting point is received as snow
            this.WaterEquivalentInMM += preciptitationInMM;
            return 0.0F; // no output.
        }
    }
}
