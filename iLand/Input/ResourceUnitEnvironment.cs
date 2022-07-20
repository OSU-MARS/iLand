﻿using System;
using System.Globalization;

namespace iLand.Input
{
    public class ResourceUnitEnvironment
    {
        public float AnnualNitrogenDeposition { get; private init; }
        public string WeatherID { get; private init; }
        // resource unit's centroid in GIS projected coordinate system
        public float GisCenterX { get; private init; }
        public float GisCenterY { get; private init; }
        public int ResourceUnitID { get; private init; }

        // parameters which obtain defaults from the project file but can be overridden in the environment file
        public float SnagBranchRootCarbon { get; private init; }
        public float SnagBranchRootCNRatio { get; private init; }
        public float SnagBranchRootDecompositionRate { get; private init; }
        public float SnagStemCarbon { get; private init; }
        public float SnagStemCNRatio { get; private init; }
        public float SnagsPerResourceUnit { get; private init; }
        public float SnagStemDecompositionRate { get; private init; }
        public float SnagHalfLife { get; private init; }

        public float SoilDepthInCm { get; private init; } // cm
        // Mualem-van Genuchten water retention parameters
        public float SoilThetaR { get; private init; } // residual soil water content, m³/m³
        public float SoilThetaS { get; private init; } // saturated soil water content, m³/m³
        public float SoilVanGenuchtenAlpha { get; private init; } // MPa, not log₁₀ transformed
        public float SoilVanGenuchtenN { get; private init; } // dimensionless
        // soil textures for estimation of Campbell (or other) water retention curve via pedotransfer regression
        public float SoilSand { get; private init; } // %
        public float SoilSilt { get; private init; } // %
        public float SoilClay { get; private init; } // %

        // ICBM2/N parameters for decomposition and flow among carbon and nitrogen pools
        public float SoilAvailableNitrogen { get; private init; }
        public float SoilEr { get; private init; } // microbial refractory efficiency
        public float SoilEl { get; private init; } // microbial labial efficiency
        public float SoilLeaching { get; private init; }
        public float SoilHumificationRate { get; private init; }
        public float SoilOrganicC { get; private init; }
        public float SoilOrganicDecompositionRate { get; private init; }
        public float SoilOrganicN { get; private init; }
        public float SoilQb { get; private init; } // soil microbe CN ratio
        public float SoilQh { get; private init; } // soil organic matter CN ratio
        public float SoilYoungLabileC { get; private init; }
        public float SoilYoungLabileDecompositionRate { get; private init; }
        public float SoilYoungLabileN { get; private init; }
        public float SoilYoungRefractoryC { get; private init; }
        public float SoilYoungRefractoryDecompositionRate { get; private init; }
        public float SoilYoungRefractoryN { get; private init; }

        public string SpeciesTableName { get; private init; }
        public bool UseDynamicAvailableNitrogen { get; private init; }

        public ResourceUnitEnvironment(ResourceUnitHeader header, string[] environmentFileRow, ResourceUnitEnvironment defaultEnvironment)
        {
            this.AnnualNitrogenDeposition = header.AnnualNitrogenDeposition >= 0 ? Single.Parse(environmentFileRow[header.AnnualNitrogenDeposition], CultureInfo.InvariantCulture) : defaultEnvironment.AnnualNitrogenDeposition;
            this.WeatherID = header.WeatherID >= 0 ? environmentFileRow[header.WeatherID] : defaultEnvironment.WeatherID;
            this.GisCenterX = Single.Parse(environmentFileRow[header.CenterX], CultureInfo.InvariantCulture); // required field
            this.GisCenterY = Single.Parse(environmentFileRow[header.CenterY], CultureInfo.InvariantCulture); // required field
            this.ResourceUnitID = Int32.Parse(environmentFileRow[header.ResourceUnitID], CultureInfo.InvariantCulture); // required field

            this.SnagBranchRootCarbon = header.SnagBranchRootCarbon >= 0 ? Single.Parse(environmentFileRow[header.SnagBranchRootCarbon], CultureInfo.InvariantCulture) : defaultEnvironment.SnagBranchRootCarbon;
            this.SnagBranchRootCNRatio = header.SnagBranchRootCNRatio >= 0 ? Single.Parse(environmentFileRow[header.SnagBranchRootCNRatio], CultureInfo.InvariantCulture) : defaultEnvironment.SnagBranchRootCNRatio;
            this.SnagStemCarbon = header.SnagStemCarbon >= 0 ? Single.Parse(environmentFileRow[header.SnagStemCarbon], CultureInfo.InvariantCulture) : defaultEnvironment.SnagStemCarbon;
            this.SnagStemCNRatio = header.SnagStemCNRatio >= 0 ? Single.Parse(environmentFileRow[header.SnagStemCNRatio], CultureInfo.InvariantCulture) : defaultEnvironment.SnagStemCNRatio;
            this.SnagStemDecompositionRate = header.SnagStemDecompositionRate >= 0 ? Single.Parse(environmentFileRow[header.SnagStemDecompositionRate], CultureInfo.InvariantCulture) : defaultEnvironment.SnagStemDecompositionRate;
            this.SnagHalfLife = header.SnagHalfLife >= 0 ? Single.Parse(environmentFileRow[header.SnagHalfLife], CultureInfo.InvariantCulture) : defaultEnvironment.SnagsPerResourceUnit;
            this.SnagsPerResourceUnit = header.SnagsPerResourceUnit >= 0 ? Single.Parse(environmentFileRow[header.SnagsPerResourceUnit], CultureInfo.InvariantCulture) : defaultEnvironment.SnagsPerResourceUnit;

            this.SoilDepthInCm = header.SoilDepthInCM >= 0 ? Single.Parse(environmentFileRow[header.SoilDepthInCM], CultureInfo.InvariantCulture) : defaultEnvironment.SoilDepthInCm;

            this.SoilThetaR = header.SoilThetaR >= 0 ? Single.Parse(environmentFileRow[header.SoilThetaR], CultureInfo.InvariantCulture) : defaultEnvironment.SoilThetaR;
            this.SoilThetaS = header.SoilThetaS >= 0 ? Single.Parse(environmentFileRow[header.SoilThetaS], CultureInfo.InvariantCulture) : defaultEnvironment.SoilThetaS;
            this.SoilVanGenuchtenAlpha = header.SoilVanGenuchtenAlpha >= 0 ? Single.Parse(environmentFileRow[header.SoilVanGenuchtenAlpha], CultureInfo.InvariantCulture) : defaultEnvironment.SoilVanGenuchtenAlpha;
            this.SoilVanGenuchtenN = header.SoilVanGenuchtenN >= 0 ? Single.Parse(environmentFileRow[header.SoilVanGenuchtenN], CultureInfo.InvariantCulture) : defaultEnvironment.SoilVanGenuchtenN;

            this.SoilClay = header.SoilClayPercentage >= 0 ? Single.Parse(environmentFileRow[header.SoilClayPercentage], CultureInfo.InvariantCulture) : defaultEnvironment.SoilClay;
            this.SoilSand = header.SoilSandPercentage >= 0 ? Single.Parse(environmentFileRow[header.SoilSandPercentage], CultureInfo.InvariantCulture) : defaultEnvironment.SoilSand;
            this.SoilSilt = header.SoilSiltPercentage >= 0 ? Single.Parse(environmentFileRow[header.SoilSiltPercentage], CultureInfo.InvariantCulture) : defaultEnvironment.SoilSilt;

            this.SoilAvailableNitrogen = header.SoilAvailableNitrogen >= 0 ? Single.Parse(environmentFileRow[header.SoilAvailableNitrogen], CultureInfo.InvariantCulture) : defaultEnvironment.SoilAvailableNitrogen;
            this.SoilEl = header.SoilEl >= 0 ? Single.Parse(environmentFileRow[header.SoilEl], CultureInfo.InvariantCulture) : defaultEnvironment.SoilEl;
            this.SoilEr = header.SoilEr >= 0 ? Single.Parse(environmentFileRow[header.SoilEr], CultureInfo.InvariantCulture) : defaultEnvironment.SoilEr;
            this.SoilLeaching = header.SoilLeaching >= 0 ? Single.Parse(environmentFileRow[header.SoilLeaching], CultureInfo.InvariantCulture) : defaultEnvironment.SoilLeaching;
            this.SoilHumificationRate = header.SoilHumificationRate >= 0 ? Single.Parse(environmentFileRow[header.SoilHumificationRate], CultureInfo.InvariantCulture) : defaultEnvironment.SoilHumificationRate;
            this.SoilOrganicC = header.SoilOrganicMatterC >= 0 ? Single.Parse(environmentFileRow[header.SoilOrganicMatterC], CultureInfo.InvariantCulture) : defaultEnvironment.SoilOrganicC;
            this.SoilOrganicDecompositionRate = header.SoilOrganicMatterDecompositionRate >= 0 ? Single.Parse(environmentFileRow[header.SoilOrganicMatterDecompositionRate], CultureInfo.InvariantCulture) : defaultEnvironment.SoilOrganicDecompositionRate;
            this.SoilOrganicN = header.SoilOrganicMatterN >= 0 ? Single.Parse(environmentFileRow[header.SoilOrganicMatterN], CultureInfo.InvariantCulture) : defaultEnvironment.SoilOrganicN;
            this.SoilQh = header.SoilQh >= 0 ? Single.Parse(environmentFileRow[header.SoilQh], CultureInfo.InvariantCulture) : defaultEnvironment.SoilQh;
            this.SoilYoungLabileC = header.SoilYoungLabileC >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungLabileC], CultureInfo.InvariantCulture) : defaultEnvironment.SoilYoungLabileC;
            this.SoilYoungLabileDecompositionRate = header.SoilYoungLabileDecompositionRate >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungLabileDecompositionRate], CultureInfo.InvariantCulture) : defaultEnvironment.SoilYoungLabileDecompositionRate;
            this.SoilYoungLabileN = header.SoilYoungLabileN >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungLabileN], CultureInfo.InvariantCulture) : defaultEnvironment.SoilYoungLabileN;
            this.SoilYoungRefractoryC = header.SoilYoungRefractoryC >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungRefractoryC], CultureInfo.InvariantCulture) : defaultEnvironment.SoilYoungRefractoryC;
            this.SoilYoungRefractoryDecompositionRate = header.SoilYoungRefractoryDecompositionRate >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungRefractoryDecompositionRate], CultureInfo.InvariantCulture) : defaultEnvironment.SoilYoungRefractoryDecompositionRate;
            this.SoilYoungRefractoryN = header.SoilYoungRefractoryN >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungRefractoryN], CultureInfo.InvariantCulture) : defaultEnvironment.SoilYoungRefractoryN;

            this.SpeciesTableName = header.SpeciesTableName >= 0 ? environmentFileRow[header.SpeciesTableName] : defaultEnvironment.SpeciesTableName;

            this.SoilQb = defaultEnvironment.SoilQb;
            this.UseDynamicAvailableNitrogen = defaultEnvironment.UseDynamicAvailableNitrogen;
        }

        public ResourceUnitEnvironment(ProjectFile.World world)
        {
            this.WeatherID = world.Weather.DefaultDatabaseTable ?? String.Empty;
            this.ResourceUnitID = -1;

            // default snag parameters which can be overridden in environment file
            this.SnagBranchRootCarbon = world.Initialization.Snags.BranchRootCarbon;
            this.SnagBranchRootCNRatio = world.Initialization.Snags.BranchRootCarbonNitrogenRatio;
            this.SnagBranchRootDecompositionRate = world.Initialization.Snags.BranchRootDecompositionRate;
            this.SnagStemDecompositionRate = world.Initialization.Snags.StemDecompositionRate; // TODO: also in species table
            this.SnagHalfLife = world.Initialization.Snags.SnagHalfLife; // TODO: also in species table
            this.SnagsPerResourceUnit = world.Initialization.Snags.SnagsPerResourceUnit;
            this.SnagStemCarbon = world.Initialization.Snags.StemCarbon;
            this.SnagStemCNRatio = world.Initialization.Snags.StemCarbonNitrogenRatio;

            // soil parameters not currently supported in environment file
            this.SoilQb = world.DefaultSoil.MicrobeCarbonNitrogenRatio;
            this.UseDynamicAvailableNitrogen = world.DefaultSoil.UseDynamicAvailableNitrogen;

            // default soil parameters which can be overridden in environment file
            this.AnnualNitrogenDeposition = world.DefaultSoil.AnnualNitrogenDeposition;
            this.SoilEl = world.DefaultSoil.MicrobialLabileEfficiency;
            this.SoilEr = world.DefaultSoil.MicrobialRefractoryEfficiency;
            this.SoilLeaching = world.DefaultSoil.NitrogenLeachingFraction;

            // default soil parameters specified in <site> rather than in <defaultSoil>
            this.SoilDepthInCm = world.DefaultSoil.Depth;

            // van Genuchten water retention curve
            this.SoilThetaR = world.DefaultSoil.ThetaR;
            this.SoilThetaS = world.DefaultSoil.ThetaS;
            this.SoilVanGenuchtenAlpha = world.DefaultSoil.VanGenuchtenAlpha;
            this.SoilVanGenuchtenN = world.DefaultSoil.VanGenuchtenN;

            // soil texture for Campbell or other water retention curve
            this.SoilSand = world.DefaultSoil.PercentSand;
            this.SoilSilt = world.DefaultSoil.PercentSilt;
            this.SoilClay = world.DefaultSoil.PercentClay;

            // ICBM2/N parameters
            this.SoilAvailableNitrogen = world.DefaultSoil.AvailableNitrogen;
            this.SoilHumificationRate = world.DefaultSoil.HumificationRate;
            this.SoilOrganicC = world.DefaultSoil.OrganicMatterCarbon;
            this.SoilOrganicDecompositionRate = world.DefaultSoil.OrganicMatterDecompositionRate;
            this.SoilOrganicN = world.DefaultSoil.OrganicMatterNitrogen;
            this.SoilQh = world.DefaultSoil.OrganicMatterCarbonNitrogenRatio;
            this.SoilYoungLabileC = world.DefaultSoil.YoungLabileCarbon;
            this.SoilYoungLabileDecompositionRate = world.DefaultSoil.YoungLabileDecompositionRate; // also in species table
            this.SoilYoungLabileN = world.DefaultSoil.YoungLabileNitrogen;
            this.SoilYoungRefractoryC = world.DefaultSoil.YoungRefractoryCarbon;
            this.SoilYoungRefractoryDecompositionRate = world.DefaultSoil.YoungRefractoryDecompositionRate; // also in species table
            this.SoilYoungRefractoryN = world.DefaultSoil.YoungRefractoryNitrogen;

            this.SpeciesTableName = world.Species.DatabaseTable ?? String.Empty;
        }

        public string GetCentroidKey()
        {
            return (int)this.GisCenterX + "_" + (int)this.GisCenterY;
        }
    }
}
