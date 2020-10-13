using System;

namespace iLand
{
    internal class Constant
    {
        public const double AutotrophicRespiration = 0.47;
        public const double BiomassCFraction = 0.5; // fraction of (dry) biomass which is carbon
        public const int DaysInLeapYear = 366;

        public const double HeightPixelArea = 100.0; // 100m2 area of a height pixel
        public const int HeightSize = 10; // size of a height grid pixel, m
        public const int LightSize = 2; // size of light grid, m
        public const int LightCellsPerHectare = 2500; // pixel/ha ( 10000 / (2*2) )
        public const int LightPerHeightSize = 5; // 10 / 2 LIF pixels per height pixel
        public const int LightPerRUsize = 50; // 100/2
        public const float RegenerationLayerHeight = 4.0F; // m
        public const double RUArea = 10000.0; // area of a resource unit (m2)
        public const int RUSize = 100; // size of resource unit, m
        public const int SeedmapSize = 20; // size of seedmap cell, m

        public const double TwoPi = 2.0 * Math.PI;
        public const double Ln2 = 0.693147180559945;
        public const double QuarterPi = 0.25 * Math.PI;
        public const double Sqrt2 = 1.4142135623731;

        public class Default
        {
            public const double CarbonDioxidePpm = 400.0;
            public const int ClimateYearsToLoadPerChunk = 100;
        }

        public class Stamp
        {
            // constants: comments may be wrong; conflicting information in C++
            public const int DbhClassCount = 70; ///< class count, see StampContainer.GetKey(): for lower dbhs classes are smaller
            public const int HeightDiameterClassMinimum = 35; ///< hd classes offset is 35: class 0 = 35-45 cm, class 1 = 45-55, ...
            public const int HeightDiameterClassCount = 16; ///< class count. highest class:  185-195 cm
            public const int HeightDiameterClassSize = 10;
        }
    }
}
