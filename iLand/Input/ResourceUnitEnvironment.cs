using System;
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

        /// <summary>
        /// rooting zone depth after volumetric exclusion of coarse fragments, cm
        /// </summary>
        /// <remarks>
        /// A resource unit's plant accessible water (mm H₂O) is the product of the unit's plant accessible soil depth and
        /// soil water content (cm³/cm³). Roots are typically assumed not to penetrate soild bedrock to access deep groundwater 
        /// so, most commonly, the plant accessible soil depth will not exceed the physical depth of the soil. Often the plant 
        /// accessible soil depth will be less than the physical soil depth for reasons such as
        /// 
        /// - The plants' root systems are not large enough to reach bedrock. This occurs when plants are small (seedlings and
        ///   perhaps saplings in iLand's case), water demand isn't sufficient to promote development of deep taproots in the
        ///   species of interest, or soils are exceptionally deep.
        /// - Soil horizons contain rocks and therefore have volumetric exclusions to plant accessible water. For example, a 10
        ///   cm horizon with 10% coarse fragment content is often approximated as having a 9 cm effective depth for the purpose
        ///   of calculating plant accessible water. Actual relations between roots, soil pores, and fragments are more complex 
        ///   and the amount of accessible water varies with the fragments' rock type, size distribution, and possibly shapes.
        /// - Pans or other high density layers which roots cannot penetrate are present, rendering lower horizons plant 
        ///   inaccessible. As with rooting depth, root penetration ability varies among species but iLand's single layer soil
        ///   model and water cycle model currently lack the ability to niche species by their root capabilities.
        /// </remarks>
        public float SoilPlantAccessibleDepthInCm { get; private init; }
        // Mualem-van Genuchten water retention parameters
        public float SoilThetaR { get; private init; } // residual soil water content, m³/m³
        public float SoilThetaS { get; private init; } // saturated soil water content, m³/m³
        public float SoilVanGenuchtenAlphaInKPa { get; private init; } // kPa, not log₁₀ transformed
        public float SoilVanGenuchtenN { get; private init; } // dimensionless
        // soil textures for estimation of Campbell (or other) water retention curve via pedotransfer regression
        public float SoilSand { get; private init; } // %
        public float SoilSilt { get; private init; } // %
        public float SoilClay { get; private init; } // %

        // ICBM2/N parameters for decomposition and flow among carbon and nitrogen pools
        public float SoilAvailableNitrogen { get; private init; }
        public float SoilEr { get; private init; } // microbial refractory efficiency
        public float SoilEl { get; private init; } // microbial labial efficiency
        public float SoilHumificationRate { get; private init; }
        public float SoilNitrogenLeachingFraction { get; private init; }
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

        public ResourceUnitEnvironment(ResourceUnitHeaderCsv header, string[] environmentFileRow, ResourceUnitEnvironment defaultEnvironment)
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

            this.SoilPlantAccessibleDepthInCm = header.SoilPlantAccessibleDepthInCm >= 0 ? Single.Parse(environmentFileRow[header.SoilPlantAccessibleDepthInCm], CultureInfo.InvariantCulture) : defaultEnvironment.SoilPlantAccessibleDepthInCm;

            this.SoilThetaR = header.SoilThetaR >= 0 ? Single.Parse(environmentFileRow[header.SoilThetaR], CultureInfo.InvariantCulture) : defaultEnvironment.SoilThetaR;
            this.SoilThetaS = header.SoilThetaS >= 0 ? Single.Parse(environmentFileRow[header.SoilThetaS], CultureInfo.InvariantCulture) : defaultEnvironment.SoilThetaS;
            this.SoilVanGenuchtenAlphaInKPa = header.SoilVanGenuchtenAlpha >= 0 ? Single.Parse(environmentFileRow[header.SoilVanGenuchtenAlpha], CultureInfo.InvariantCulture) : defaultEnvironment.SoilVanGenuchtenAlphaInKPa;
            this.SoilVanGenuchtenN = header.SoilVanGenuchtenN >= 0 ? Single.Parse(environmentFileRow[header.SoilVanGenuchtenN], CultureInfo.InvariantCulture) : defaultEnvironment.SoilVanGenuchtenN;

            this.SoilClay = header.SoilClayPercentage >= 0 ? Single.Parse(environmentFileRow[header.SoilClayPercentage], CultureInfo.InvariantCulture) : defaultEnvironment.SoilClay;
            this.SoilSand = header.SoilSandPercentage >= 0 ? Single.Parse(environmentFileRow[header.SoilSandPercentage], CultureInfo.InvariantCulture) : defaultEnvironment.SoilSand;
            this.SoilSilt = header.SoilSiltPercentage >= 0 ? Single.Parse(environmentFileRow[header.SoilSiltPercentage], CultureInfo.InvariantCulture) : defaultEnvironment.SoilSilt;

            this.SoilAvailableNitrogen = header.SoilAvailableNitrogen >= 0 ? Single.Parse(environmentFileRow[header.SoilAvailableNitrogen], CultureInfo.InvariantCulture) : defaultEnvironment.SoilAvailableNitrogen;
            this.SoilEl = header.SoilEl >= 0 ? Single.Parse(environmentFileRow[header.SoilEl], CultureInfo.InvariantCulture) : defaultEnvironment.SoilEl;
            this.SoilEr = header.SoilEr >= 0 ? Single.Parse(environmentFileRow[header.SoilEr], CultureInfo.InvariantCulture) : defaultEnvironment.SoilEr;
            this.SoilHumificationRate = header.SoilHumificationRate >= 0 ? Single.Parse(environmentFileRow[header.SoilHumificationRate], CultureInfo.InvariantCulture) : defaultEnvironment.SoilHumificationRate;
            this.SoilNitrogenLeachingFraction = header.SoilLeaching >= 0 ? Single.Parse(environmentFileRow[header.SoilLeaching], CultureInfo.InvariantCulture) : defaultEnvironment.SoilNitrogenLeachingFraction;
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

        public ResourceUnitEnvironment(ResourceUnitArrowBatch arrowBatch, int index, ResourceUnitEnvironment defaultEnvironment)
        {
            this.AnnualNitrogenDeposition = arrowBatch.AnnualNitrogenDeposition != null ? arrowBatch.AnnualNitrogenDeposition.Values[index] : defaultEnvironment.AnnualNitrogenDeposition;
            this.WeatherID = arrowBatch.WeatherID != null ? arrowBatch.WeatherID.GetString(index) : defaultEnvironment.WeatherID;
            this.GisCenterX = arrowBatch.CenterX.Values[index]; // required field
            this.GisCenterY = arrowBatch.CenterY.Values[index]; // required field
            this.ResourceUnitID = arrowBatch.ResourceUnitID.Values[index]; // required field

            this.SnagBranchRootCarbon = arrowBatch.SnagBranchRootCarbon != null ? arrowBatch.SnagBranchRootCarbon.Values[index] : defaultEnvironment.SnagBranchRootCarbon;
            this.SnagBranchRootCNRatio = arrowBatch.SnagBranchRootCNRatio != null ? arrowBatch.SnagBranchRootCNRatio.Values[index] : defaultEnvironment.SnagBranchRootCNRatio;
            this.SnagStemCarbon = arrowBatch.SnagStemCarbon != null ? arrowBatch.SnagStemCarbon.Values[index] : defaultEnvironment.SnagStemCarbon;
            this.SnagStemCNRatio = arrowBatch.SnagStemCNRatio != null ? arrowBatch.SnagStemCNRatio.Values[index] : defaultEnvironment.SnagStemCNRatio;
            this.SnagStemDecompositionRate = arrowBatch.SnagStemDecompositionRate != null ? arrowBatch.SnagStemDecompositionRate.Values[index] : defaultEnvironment.SnagStemDecompositionRate;
            this.SnagHalfLife = arrowBatch.SnagHalfLife != null ? arrowBatch.SnagHalfLife.Values[index] : defaultEnvironment.SnagsPerResourceUnit;
            this.SnagsPerResourceUnit = arrowBatch.SnagsPerResourceUnit != null ? arrowBatch.SnagsPerResourceUnit.Values[index] : defaultEnvironment.SnagsPerResourceUnit;

            this.SoilPlantAccessibleDepthInCm = arrowBatch.SoilPlantAccessibleDepthInCm != null ? arrowBatch.SoilPlantAccessibleDepthInCm.Values[index] : defaultEnvironment.SoilPlantAccessibleDepthInCm;

            this.SoilThetaR = arrowBatch.SoilThetaR != null ? arrowBatch.SoilThetaR.Values[index] : defaultEnvironment.SoilThetaR;
            this.SoilThetaS = arrowBatch.SoilThetaS != null ? arrowBatch.SoilThetaS.Values[index] : defaultEnvironment.SoilThetaS;
            this.SoilVanGenuchtenAlphaInKPa = arrowBatch.SoilVanGenuchtenAlpha != null ? arrowBatch.SoilVanGenuchtenAlpha.Values[index] : defaultEnvironment.SoilVanGenuchtenAlphaInKPa;
            this.SoilVanGenuchtenN = arrowBatch.SoilVanGenuchtenN != null ? arrowBatch.SoilVanGenuchtenN.Values[index] : defaultEnvironment.SoilVanGenuchtenN;

            this.SoilClay = arrowBatch.SoilClayPercentage != null ? arrowBatch.SoilClayPercentage.Values[index] : defaultEnvironment.SoilClay;
            this.SoilSand = arrowBatch.SoilSandPercentage != null ? arrowBatch.SoilSandPercentage.Values[index] : defaultEnvironment.SoilSand;
            this.SoilSilt = arrowBatch.SoilSiltPercentage != null ? arrowBatch.SoilSiltPercentage.Values[index] : defaultEnvironment.SoilSilt;

            this.SoilAvailableNitrogen = arrowBatch.SoilAvailableNitrogen != null ? arrowBatch.SoilAvailableNitrogen.Values[index] : defaultEnvironment.SoilAvailableNitrogen;
            this.SoilEl = arrowBatch.SoilEl != null ? arrowBatch.SoilEl.Values[index] : defaultEnvironment.SoilEl;
            this.SoilEr = arrowBatch.SoilEr != null ? arrowBatch.SoilEr.Values[index] : defaultEnvironment.SoilEr;
            this.SoilNitrogenLeachingFraction = arrowBatch.SoilLeaching != null ? arrowBatch.SoilLeaching.Values[index] : defaultEnvironment.SoilNitrogenLeachingFraction;
            this.SoilHumificationRate = arrowBatch.SoilHumificationRate != null ? arrowBatch.SoilHumificationRate.Values[index] : defaultEnvironment.SoilHumificationRate;
            this.SoilOrganicC = arrowBatch.SoilOrganicMatterC != null ? arrowBatch.SoilOrganicMatterC.Values[index] : defaultEnvironment.SoilOrganicC;
            this.SoilOrganicDecompositionRate = arrowBatch.SoilOrganicMatterDecompositionRate != null ? arrowBatch.SoilOrganicMatterDecompositionRate.Values[index] : defaultEnvironment.SoilOrganicDecompositionRate;
            this.SoilOrganicN = arrowBatch.SoilOrganicMatterN != null ? arrowBatch.SoilOrganicMatterN.Values[index] : defaultEnvironment.SoilOrganicN;
            this.SoilQh = arrowBatch.SoilQh != null ? arrowBatch.SoilQh.Values[index] : defaultEnvironment.SoilQh;
            this.SoilYoungLabileC = arrowBatch.SoilYoungLabileC != null ? arrowBatch.SoilYoungLabileC.Values[index] : defaultEnvironment.SoilYoungLabileC;
            this.SoilYoungLabileDecompositionRate = arrowBatch.SoilYoungLabileDecompositionRate != null ? arrowBatch.SoilYoungLabileDecompositionRate.Values[index] : defaultEnvironment.SoilYoungLabileDecompositionRate;
            this.SoilYoungLabileN = arrowBatch.SoilYoungLabileN != null ? arrowBatch.SoilYoungLabileN.Values[index] : defaultEnvironment.SoilYoungLabileN;
            this.SoilYoungRefractoryC = arrowBatch.SoilYoungRefractoryC != null ? arrowBatch.SoilYoungRefractoryC.Values[index] : defaultEnvironment.SoilYoungRefractoryC;
            this.SoilYoungRefractoryDecompositionRate = arrowBatch.SoilYoungRefractoryDecompositionRate != null ? arrowBatch.SoilYoungRefractoryDecompositionRate.Values[index] : defaultEnvironment.SoilYoungRefractoryDecompositionRate;
            this.SoilYoungRefractoryN = arrowBatch.SoilYoungRefractoryN != null ? arrowBatch.SoilYoungRefractoryN.Values[index] : defaultEnvironment.SoilYoungRefractoryN;

            this.SpeciesTableName = arrowBatch.SpeciesTableName != null ? arrowBatch.SpeciesTableName.GetString(index) : defaultEnvironment.SpeciesTableName;

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
            this.SoilNitrogenLeachingFraction = world.DefaultSoil.NitrogenLeachingFraction;

            // default soil parameters specified in <site> rather than in <defaultSoil>
            this.SoilPlantAccessibleDepthInCm = world.DefaultSoil.PlantAccessibleDepthInCm;

            // van Genuchten water retention curve
            this.SoilThetaR = world.DefaultSoil.ThetaR;
            this.SoilThetaS = world.DefaultSoil.ThetaS;
            this.SoilVanGenuchtenAlphaInKPa = world.DefaultSoil.VanGenuchtenAlpha;
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
