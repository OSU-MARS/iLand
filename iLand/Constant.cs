using System;

namespace iLand
{
    internal static class Constant
    {
        public const int AllTreeSpeciesCode = 0;

        public const int DaysInDecade = 3652; // integer truncation of 10 years * 365.2425 days/year, could also use 3653 for decades with three leap years
        public const int DaysInLeapYear = 366;
        public const int DaysInYear = 365;

        public const int DefaultStandID = 0; // lowest valid stand ID, any negative IDs in stand raster are treated as no data or no stand
        public const float DryBiomassCarbonFraction = 0.5F; // fraction of dry biomass which is carbon
        public const int EvergreenLeafPhenologyID = 0;

        public const int HeightCellsPerRUWidth = 10; // height cells per resource unit side length, used for torus positioning
        public const int HeightCellAreaInM2 = 100; // 100 m² area of a height pixel
        public const int HeightCellSizeInM = 10; // size of height grid cells, m
        public const int LightCellSizeInM = 2; // size of light grid cells, m
        public const int LightCellsPerHectare = 2500; // 10000 m² / (2 m * 2 m)
        public const int LightCellsPerSeedmapCellWidth = 10; // 20 m / 2 m; keep in sync with seedmap and light cell sizes
        public const int LightCellsPerHeightCellWidth = 5; // 10 m / 2 m LIF pixels per height pixel; keep in sync with light and height cell sizes
        public const int LightCellsPerRUWidth = 50; // 100 m / 2 m

        public const int NoDataInt32 = Int32.MinValue;
        public const float NoDataFloat = Single.NaN;

        public const float RegenerationLayerHeight = 4.0F; // m
        public const float ResourceUnitAreaInM2 = 10000.0F; // area of a resource unit, m²
        public const int ResourceUnitSizeInM = 100; // size of resource unit, m
        public const int SeedmapCellSizeInM = 20; // size of seedmap cell, m
        public const int TimeStepInYears = 1;

        public const float Ln2 = 0.693147180559945F;
        public const int MonthsInYear = 12;
        public const float QuarterPi = 0.25F * MathF.PI;
        public const float SquareMetersPerHectare = 10000.0F;
        public const float Sqrt2 = 1.4142135623731F;

        public static class Data
        {
            public const int AnnualAllocationIncrement = 25; // 25 years
            public const string DefaultSpeciesTable = "species";
            public const int MonthlyAllocationIncrement = 12 * 25; // 25 years
        }

        public static class File
        {
            public const string CsvExtension = ".csv";
            public const int DefaultBufferSize = 128 * 1024; // 128 kB
            public const string FeatherExtension = ".feather";
            public const string PicusExtension = ".picus";
            public const string ReaderStampFileName = "readerstamp.feather";
            public const string SqliteExtension = ".sqlite";
        }

        public static class LightStamp
        {
            public const int HeightDiameterClassMinimum = 35; // hd ratio classes offset is 35: class 0 < 45, class 1 45-55, ...
            public const int HeightDiameterClassSize = 10;
        }

        public static class Limit
        {
            public const float MonthlyTotalSolarRadiationMaximum = 1250.0F; // MJ/m²
            public const float DailyTotalSolarRadiationMinimum = 0.0F; // MJ/m², set to zero for now to accept no data coding errors in weather series
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

        public static class Simd128
        {
            public const int Width32 = 4;
        }
    }
}
