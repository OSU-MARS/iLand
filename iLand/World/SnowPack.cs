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
            Returns the total amount of water that exits the snowpack (precipitation, snow melt) over the timestep */
        public float FlowPrecipitationTimestep(float totalThroughfallInMM, float meanTemperatureInC)
        {
            // TODO: account for day to day variability in throughfall and temperature
            if (meanTemperatureInC > this.MeltTemperatureInC)
            {
                if (this.WaterEquivalentInMM == 0.0F)
                {
                    return totalThroughfallInMM; // no snow to melt, so all throughfall becomes potential infiltration
                }

                // melting from rain on snow event
                const float meltingCoefficient = 0.7F; // mm/C
                float totalSnowmelt = MathF.Min((meanTemperatureInC - this.MeltTemperatureInC) * meltingCoefficient, this.WaterEquivalentInMM);
                this.WaterEquivalentInMM -= totalSnowmelt;
                return totalThroughfallInMM + totalSnowmelt;
            }

            // assume all precipitation on days below melting point is received as snow
            this.WaterEquivalentInMM += totalThroughfallInMM;
            return 0.0F; // no output
        }
    }
}
