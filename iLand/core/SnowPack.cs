using System;

namespace iLand.core
{
    internal class SnowPack
    {
        public double Temperature { get; set; } ///< Threshold temperature for snowing / snow melt
        public double WaterEquivalent { get; set; } ///< height of snowpack (mm water column)

        public SnowPack()
        {
            WaterEquivalent = 0.0;
        }

        public void Setup() { WaterEquivalent = 0.0; }

        /// additional precipitation (e.g. non evaporated water of canopy interception).
        public double Add(double preciptitation_mm, double temperature)
        {
            // do nothing for temps > 0 C
            if (temperature > Temperature)
            {
                return preciptitation_mm;
            }

            // temps < 0 C: add to snow
            WaterEquivalent += preciptitation_mm;
            return 0.0;
        }

        /// process the snow layer. Returns the mm of preciptitation/melt water that leaves the snow layer.
        /** calculates the input/output of water that is stored in the snow pack.
            The approach is similar to Picus 1.3 and ForestBGC (Running, 1988).
            Returns the amount of water that exits the snowpack (precipitation, snow melt) */
        public double Flow(double preciptitation_mm, double temperature)
        {
            if (temperature > Temperature)
            {
                if (WaterEquivalent == 0.0)
                {
                    return preciptitation_mm; // no change
                }
                else
                {
                    // snow melts
                    const double melting_coefficient = 0.7; // mm/C
                    double melt = Math.Min((temperature - Temperature) * melting_coefficient, WaterEquivalent);
                    WaterEquivalent -= melt;
                    return preciptitation_mm + melt;
                }
            }
            else
            {
                // snow:
                WaterEquivalent += preciptitation_mm;
                return 0.0; // no output.
            }
        }
    }
}
