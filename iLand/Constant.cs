using System;

namespace iLand
{
    internal class Constant
    {
        public const double AutotrophicRespiration = 0.47;
        public const double BiomassCFraction = 0.5; // fraction of (dry) biomass which is carbon

        public const int HeightPerRUsize = 10; // 100/10 height pixels per resource unit
        public const double HeightPixelArea = 100.0; // 100m2 area of a height pixel
        public const int HeightSize = 10; // size of a height grid pixel (m)
        public const int LightSize = 2; // size of light grid (m)
        public const int LightCellsPerHectare = 2500; // pixel/ha ( 10000 / (2*2) )
        public const int LightPerHeightSize = 5; // 10 / 2 LIF pixels per height pixel
        public const int LightPerRUsize = 50; // 100/2
        public const double RUArea = 10000.0; // area of a resource unit (m2)
        public const int RUSize = 100; // size of resource unit (m)

        public const double TwoPi = 2.0 * Math.PI;
        public const double Ln2 = 0.693147180559945;
        public const double QuarterPi = 0.25 * Math.PI;
        public const double Sqrt2 = 1.4142135623731;

    }
}
