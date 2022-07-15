using System;

namespace iLand
{
    internal static class Constant
    {
        public const float BiomassCFraction = 0.5F; // fraction of (dry) biomass which is carbon
        public const int DaysInDecade = 3652; // integer truncation of 10 years * 365.2425 days/year, could also use 3653 for decades with three leap years
        public const int DaysInLeapYear = 366;
        public const int DaysInYear = 365;

        public const int DefaultStandID = 0; // lowest valid stand ID, any negative IDs in stand raster are treated as no data or no stand
        public const int EvergreenLeafPhenologyID = 0;

        public const int HeightCellsPerRUWidth = 10; // height cells per resource unit side length, used for torus positioning
        public const int HeightCellAreaInM2 = 100; // 100 m² area of a height pixel
        public const int HeightCellSizeInM = 10; // size of height grid cells, m
        public const int LightCellSizeInM = 2; // size of light grid cells, m
        public const int LightCellsPerHectare = 2500; // pixel/ha ( 10000 / (2*2) )
        public const int LightCellsPerSeedmapCellWidth = 10; // 20 m / 2 m; keep in sync with seedmap and light cell sizes
        public const int LightCellsPerHeightCellWidth = 5; // 10 / 2 LIF pixels per height pixel; keep in sync with light and height cell sizes
        public const int LightCellsPerRUWidth = 50; // 100/2

        public const int NoDataInt32 = Int32.MinValue;
        public const float NoDataSingle = Single.NaN;

        public const float RegenerationLayerHeight = 4.0F; // m
        public const float ResourceUnitAreaInM2 = 10000.0F; // area of a resource unit, m²
        public const int ResourceUnitSizeInM = 100; // size of resource unit, m
        public const int SeedmapCellSizeInM = 20; // size of seedmap cell, m
        public const int TimeStepInYears = 1;

        public const float Ln2 = 0.693147180559945F;
        public const int MonthsInYear = 12;
        public const float QuarterPi = 0.25F * MathF.PI;
        public const float Sqrt2 = 1.4142135623731F;

        public static class Data
        {
            public const string DefaultSpeciesTable = "species";
            public const int MonthlyWeatherAllocationIncrement = 12 * 25; // 25 years
        }

        public static class File
        {
            public const int DefaultBufferSize = 128 * 1024; // 128 kB
        }

        public static class Limit
        {
            public const float DailySolarRadiation = 50.0F; // MJ/m²
            public const float MonthlyPrecipitationInMM = 9500.0F; // mm (Cherrapunji, July 1861)
            public const float TemperatureMax = 50.0F;
            public const float TemperatureMin = -70.0F;
            public const float VaporPressureDeficitInKPa = 10.0F; // kPa
            public static readonly int YearMax = DateTime.MaxValue.Year;
            public static readonly int YearMin = DateTime.MinValue.Year;
        }

        public static class Sapling
        {
            public const int HeightClasses = 41;
            public const float HeightClassSize = 0.1F; // m
            public const float MaximumHeight = 4.0F; // m
            public const float MinimumHeight = 0.05F; // m
        }

        public static class Simd128x4
        {
            public const int Width = 4;
        }

        public static class Stamp
        {
            // constants: comments may be wrong; conflicting information in C++
            public const int DbhClassCount = 70; // class count, see StampContainer.GetKey(): for lower dbhs classes are smaller
            public const int HeightDiameterClassMinimum = 35; // hd classes offset is 35: class 0 = 35-45 cm, class 1 = 45-55, ...
            public const int HeightDiameterClassCount = 16; // class count. highest class:  185-195 cm
            public const int HeightDiameterClassSize = 10;
        }
    }
}
