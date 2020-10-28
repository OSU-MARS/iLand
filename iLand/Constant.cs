using System;

namespace iLand
{
    internal static class Constant
    {
        public const double AutotrophicRespiration = 0.47;
        public const float BiomassCFraction = 0.5F; // fraction of (dry) biomass which is carbon
        public const int DaysInLeapYear = 366;

        public const int HeightSizePerRU = 10; // height cells per resource unit side length, used for torus positioning
        public const float HeightPixelArea = 100.0F; // 100m2 area of a height pixel
        public const int HeightSize = 10; // size of a height grid pixel, m
        public const int LightSize = 2; // size of light grid, m
        public const int LightCellsPerHectare = 2500; // pixel/ha ( 10000 / (2*2) )
        public const int LightCellsPerSeedmapSize = 20; // 20 m / 2 m
        public const int LightCellsPerHeightSize = 5; // 10 / 2 LIF pixels per height pixel
        public const int LightCellsPerRUsize = 50; // 100/2
        public const float RegenerationLayerHeight = 4.0F; // m
        public const float RUArea = 10000.0F; // area of a resource unit (m2)
        public const int RUSize = 100; // size of resource unit, m
        public const int SeedmapSize = 20; // size of seedmap cell, m
        public const int TimeStepInYears = 1;

        public const double TwoPi = 2.0 * Math.PI;
        public const float Ln2 = 0.693147180559945F;
        public const int MonthsInYear = 12;
        public const float QuarterPi = 0.25F * MathF.PI;
        public const double Sqrt2 = 1.4142135623731;

        public static class Sapling
        {
            public const int HeightClasses = 41;
            public const float HeightClassSize = 0.1F; // m
            public const float MaximumHeight = 4.0F; // m
            public const float MinimumHeight = 0.05F; // m
        }

        public static class Setting
        {
            public const string SpeciesTable = "model.species.source";

            public static class Climate
            {
                public const string CarbonDioxidePpm = "model.climate.co2concentration";
                public const string Name = "model.climate.tableName";
                public const string PrecipitationMultiplier = "precipitationShift";
                public const string TemperatureShift = "temperatureShift";
                public const string YearsPerLoad = "batchYears";
            }

            public static class Snag
            {
                public const string OtherC = "model.initialization.snags.otherC";
                public const string OtherCN = "model.initialization.snags.otherCN";
                public const string SwdC = "model.initialization.snags.swdC";
                public const string SwdCN = "model.initialization.snags.swdCN";
                public const string SwdDecompositionRate = "model.initialization.snags.swdDecompRate";
                public const string SwdHalfLife = "model.initialization.snags.swdHalfLife";
                public const string SwdN = "model.initialization.snags.swdCount";
            }

            public static class Soil
            {
                public const string AnnualNitrogenDeposition = "model.settings.soil.nitrogenDeposition";
                public const string AvailableNitrogen = "model.site.availableNitrogen";
                public const string Depth = "model.site.soilDepth";
                public const string El = "model.settings.soil.el";
                public const string Er = "model.settings.soil.er";
                public const string Leaching = "model.settings.soil.leaching";
                public const string HumificationRate = "model.site.soilHumificationRate";
                public const string OrganicMatterC = "model.site.somC";
                public const string OrganicMatterDecompositionRate = "model.site.somDecompRate";
                public const string OrganincMatterN = "model.site.somN";
                public const string PercentClay = "model.site.pctClay";
                public const string PercentSand = "model.site.pctSand";
                public const string PercentSilt = "model.site.pctSilt";
                public const string Qb = "model.settings.soil.qb";
                public const string Qh = "model.settings.soil.qh";

                public const string SwhDbhClass12 = "model.settings.soil.swdDBHClass12";
                public const string SwhDbhClass23 = "model.settings.soil.swdDBHClass23";
                public const string UseDynamicAvailableNitrogen = "model.settings.soil.useDynamicAvailableNitrogen";
                public const string YoungLabileC = "model.site.youngLabileC";
                public const string YoungLabileDecompositionRate = "model.site.youngLabileDecompRate";
                public const string YoungLabileN = "model.site.youngLabileN";
                public const string YoungRefractoryC = "model.site.youngRefractoryC";
                public const string YoungRefractoryDecompositionRate = "model.site.youngRefractoryDecompRate";
                public const string YoungRefractoryN = "model.site.youngRefractoryN";
            }
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
