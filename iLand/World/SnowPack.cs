using System;

namespace iLand.World
{
    internal class SnowPack
    {
        public float MeltTemperature { get; set; } // Threshold temperature for snowing / snow melt
        public float WaterEquivalent { get; set; } // height of snowpack (mm water column)

        public SnowPack()
        {
            this.MeltTemperature = 0.0F;
            this.WaterEquivalent = 0.0F;
        }

        /// additional precipitation (e.g. non evaporated water of canopy interception).
        public float AddSnowWaterEquivalent(float preciptitationInMM, float temperatureC)
        {
            // no snow to add if temp >0 C
            if (temperatureC > this.MeltTemperature)
            {
                return preciptitationInMM;
            }

            // temps < 0 C: add to snow
            this.WaterEquivalent += preciptitationInMM;
            return 0.0F;
        }

        /// process the snow layer. Returns the mm of preciptitation/melt water that leaves the snow layer.
        /** calculates the input/output of water that is stored in the snow pack.
            The approach is similar to Picus 1.3 and ForestBGC (Running, 1988).
            Returns the amount of water that exits the snowpack (precipitation, snow melt) */
        public float Flow(float preciptitationInMM, float temperatureC)
        {
            if (temperatureC > this.MeltTemperature)
            {
                if (this.WaterEquivalent == 0.0F)
                {
                    return preciptitationInMM; // no change
                }
                else
                {
                    // melting from rain on snow event
                    const float meltingCoefficient = 0.7F; // mm/C
                    float melt = MathF.Min((temperatureC - this.MeltTemperature) * meltingCoefficient, WaterEquivalent);
                    this.WaterEquivalent -= melt;
                    return preciptitationInMM + melt;
                }
            }
            else
            {
                // snow:
                this.WaterEquivalent += preciptitationInMM;
                return 0.0F; // no output.
            }
        }
    }
}
