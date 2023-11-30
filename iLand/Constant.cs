using iLand.Extensions;
using System;
using System.Runtime.Intrinsics;

namespace iLand
{
    internal static class Constant
    {
        public const int AllTreeSpeciesCode = 0;

        public const UInt32 DefaultStandID = 0; // lowest valid stand ID, any negative IDs in stand raster are treated as no data or no stand
        public const float DryBiomassCarbonFraction = 0.5F; // fraction of dry biomass which is carbon
        public const int EvergreenLeafPhenologyID = 0;

        public const float MinimumLightIntensity = 0.02F;
        public static readonly Vector128<float> MinimumLightIntensity128 = AvxExtensions.BroadcastScalarToVector128(Constant.MinimumLightIntensity);
        public static readonly Vector256<float> MinimumLightIntensity256 = AvxExtensions.BroadcastScalarToVector256(Constant.MinimumLightIntensity);
        public const int NoDataInt32 = Int32.MinValue;
        public const float NoDataFloat = Single.NaN;
        public const UInt32 NoDataUInt32 = UInt32.MaxValue;

        public const float RegenerationLayerHeight = 4.0F; // m, also controls Constant.Sapling.MaximumHeight
        public const float SquareMetersPerHectare = 10000.0F;

        public static class Data
        {
            public const int DefaultAnnualAllocationIncrement = 25; // 25 years
            public const int DefaultMonthlyAllocationIncrement = 12 * 25; // also 25 years
            public const int DefaultResourceUnitAllocationIncrement = 256;
            public const string DefaultSpeciesTable = "species";
            public const int DefaultTreeAllocationIncrement = 1000;
            public const int MaxResourceUnitTreeBatchSize = 200;
            public const int MinimumResourceUnitsPerLoggingThread = 50;
            public const int MinimumStandsPerLoggingThread = 50;
            public const int MinimumTreesPerThread = 50 * 1000;
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

        public static class Grid
        {
            public const int DefaultWorldBufferWidthInM = 80; // see WorldGeometry.cs
            public const int DominantHeightFieldBufferWidthInHeightCells = 12; // HeightCellsPerRUWidth + 1 + 1
            public const float FullLightIntensity = 1.0F; // no shade
            public const int HeightCellsPerRUWidth = 10; // height cells per resource unit side length, used for torus positioning
            public const int HeightCellAreaInM2 = 100; // 100 m² area of a height pixel
            public const int HeightCellSizeInM = 10; // size of height grid cells, m
            public const int LightCellSizeInM = 2; // size of light grid cells, m
            public const int LightCellsPerHectare = 2500; // 10000 m² / (2 m * 2 m)
            public const int LightCellsPerSeedmapCellWidth = 10; // 20 m / 2 m; keep in sync with seedmap and light cell sizes
            public const int LightCellsPerHeightCellWidth = 5; // 10 m / 2 m LIF pixels per height pixel; keep in sync with light and height cell sizes
            public const int LightCellsPerRUWidth = 50; // 100 m / 2 m
            public const int MaxLightStampSizeInLightCells = 64; // see LightStampSize.cs
            public const float ResourceUnitAreaInM2 = 10000.0F; // area of a resource unit, m²
            public const int ResourceUnitSizeInM = 100; // size of resource unit, m
            public const int SeedmapCellSizeInM = 20; // size of seedmap cell, m
            public const int SeedmapCellsPerRUWidth = 5; // 100 m / 20 m
            public const float TreeNudgeIntoResourceUnitInM = 0.01F;
        }

        public static class Grid128F
        {
            public static readonly Vector128<float> LightCellSizeInM = AvxExtensions.BroadcastScalarToVector128((float)Constant.Grid.LightCellSizeInM);
        }

        public static class Grid256F
        {
            public static readonly Vector256<float> LightCellSizeInM = AvxExtensions.BroadcastScalarToVector256((float)Constant.Grid.LightCellSizeInM);
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

        public static class Math
        {
            public const float Ln2 = 0.693147180559945F;
        }

        public static class Sapling
        {
            public const int HeightClasses = 41;
            public const float HeightClassSize = 0.1F; // m
            public const float MinimumHeight = 0.05F; // m, maximum height is Constant.RegenerationLayerHeight
        }

        public static class Time
        {
            public const int DaysInDecade = 3652; // integer truncation of 10 years * 365.2425 days/year, could also use 3653 for decades with three leap years
            public const int DaysInLeapYear = 366;
            public const int DaysInYear = 365;
            public const int MonthsInYear = 12;
            public const int TimeStepInYears = 1;
        }
    }
}
