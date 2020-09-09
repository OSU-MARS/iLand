using System;

namespace iLand.core
{
    internal class SnowPack
    {
        private double mSnowPack; ///< height of snowpack (mm water column)
        public double mSnowTemperature { get; set; } ///< Threshold temperature for snowing / snow melt

        public void setup() { mSnowPack = 0.0; }
        public void setSnow(double snow_mm) { mSnowPack = snow_mm; }
        public double snowPack() { return mSnowPack; } ///< current snowpack height (mm)

        public SnowPack()
        {
            mSnowPack = 0.0;
        }

        /// process the snow layer. Returns the mm of preciptitation/melt water that leaves the snow layer.
        /** calculates the input/output of water that is stored in the snow pack.
            The approach is similar to Picus 1.3 and ForestBGC (Running, 1988).
            Returns the amount of water that exits the snowpack (precipitation, snow melt) */
        public double flow(double preciptitation_mm, double temperature)
        {
            if (temperature > mSnowTemperature)
            {
                if (mSnowPack == 0.0)
                {
                    return preciptitation_mm; // no change
                }
                else
                {
                    // snow melts
                    const double melting_coefficient = 0.7; // mm/C
                    double melt = Math.Min((temperature - mSnowTemperature) * melting_coefficient, mSnowPack);
                    mSnowPack -= melt;
                    return preciptitation_mm + melt;
                }
            }
            else
            {
                // snow:
                mSnowPack += preciptitation_mm;
                return 0.0; // no output.
            }
        }

        /// additional precipitation (e.g. non evaporated water of canopy interception).
        public double add(double preciptitation_mm, double temperature)
        {
            // do nothing for temps > 0 C
            if (temperature > mSnowTemperature)
            {
                return preciptitation_mm;
            }

            // temps < 0 C: add to snow
            mSnowPack += preciptitation_mm;
            return 0.0;
        }
    }
}
