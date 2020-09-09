using System;

namespace iLand
{
    internal class Constant
    {
        public const int cPxSize = 2; // size of light grid (m)
        public const int cRUSize = 100; // size of resource unit (m)
        public const double cRUArea = 10000.0; // area of a resource unit (m2)
        public const int cHeightSize = 10; // size of a height grid pixel (m)
        public const int cPxPerHeight = 5; // 10 / 2 LIF pixels per height pixel
        public const int cPxPerRU = 50; // 100/2
        public const int cHeightPerRU = 10; // 100/10 height pixels per resource unit
        public const int cPxPerHectare = 2500; // pixel/ha ( 10000 / (2*2) )
        public const double cHeightPixelArea = 100.0; // 100m2 area of a height pixel

        public const double PI_2 = 2.0 * Math.PI;
        public const double M_LN2 = 0.693147180559945;
        public const double M_PI_4 = 0.25 * Math.PI;
        public const double M_SQRT2 = 1.4142135623731;

        // other constants
        public const double biomassCFraction = 0.5; // fraction of (dry) biomass which is carbon
        public const double cAutotrophicRespiration = 0.47;
    }
}
