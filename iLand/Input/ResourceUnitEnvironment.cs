using System;

namespace iLand.Input
{
    public class ResourceUnitEnvironment
    {
        public float AnnualNitrogenDeposition { get; private init; }
        public string ClimateID { get; private init; }
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

        public float SoilAvailableNitrogen { get; private init; }
        public float SoilDepthInCM { get; private init; } // cm
        public float SoilEr { get; private init; }
        public float SoilEl { get; private init; }
        public float SoilLeaching { get; private init; }
        public float SoilHumificationRate { get; private init; }
        public float SoilOrganicC { get; private init; }
        public float SoilOrganicDecompositionRate { get; private init; }
        public float SoilOrganicN { get; private init; }
        public float SoilQb { get; private init; }
        public float SoilQh { get; private init; }
        public float SoilSand { get; private init; }
        public float SoilSilt { get; private init; }
        public float SoilClay { get; private init; }
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
            this.AnnualNitrogenDeposition = header.AnnualNitrogenDeposition >= 0 ? Single.Parse(environmentFileRow[header.AnnualNitrogenDeposition]) : defaultEnvironment.AnnualNitrogenDeposition;
            this.ClimateID = header.ClimateID >= 0 ? environmentFileRow[header.ClimateID] : defaultEnvironment.ClimateID;
            this.GisCenterX = Single.Parse(environmentFileRow[header.CenterX]); // required field
            this.GisCenterY = Single.Parse(environmentFileRow[header.CenterY]); // required field
            this.ResourceUnitID = Int32.Parse(environmentFileRow[header.ResourceUnitID]); // required field

            this.SnagBranchRootCarbon = header.SnagBranchRootCarbon >= 0 ? Single.Parse(environmentFileRow[header.SnagBranchRootCarbon]) : defaultEnvironment.SnagBranchRootCarbon;
            this.SnagBranchRootCNRatio = header.SnagBranchRootCNRatio >= 0 ? Single.Parse(environmentFileRow[header.SnagBranchRootCNRatio]) : defaultEnvironment.SnagBranchRootCNRatio;
            this.SnagStemCarbon = header.SnagStemCarbon >= 0 ? Single.Parse(environmentFileRow[header.SnagStemCarbon]) : defaultEnvironment.SnagStemCarbon;
            this.SnagStemCNRatio = header.SnagStemCNRatio >= 0 ? Single.Parse(environmentFileRow[header.SnagStemCNRatio]) : defaultEnvironment.SnagStemCNRatio;
            this.SnagStemDecompositionRate = header.SnagStemDecompositionRate >= 0 ? Single.Parse(environmentFileRow[header.SnagStemDecompositionRate]) : defaultEnvironment.SnagStemDecompositionRate;
            this.SnagHalfLife = header.SnagHalfLife >= 0 ? Single.Parse(environmentFileRow[header.SnagHalfLife]) : defaultEnvironment.SnagsPerResourceUnit;
            this.SnagsPerResourceUnit = header.SnagsPerResourceUnit >= 0 ? Single.Parse(environmentFileRow[header.SnagsPerResourceUnit]) : defaultEnvironment.SnagsPerResourceUnit;

            this.SoilAvailableNitrogen = header.SoilAvailableNitrogen >= 0 ? Single.Parse(environmentFileRow[header.SoilAvailableNitrogen]) : defaultEnvironment.SoilAvailableNitrogen;
            this.SoilDepthInCM = header.SoilDepthInCM >= 0 ? Single.Parse(environmentFileRow[header.SoilDepthInCM]) : defaultEnvironment.SoilDepthInCM;
            this.SoilEl = header.SoilEl >= 0 ? Single.Parse(environmentFileRow[header.SoilEl]) : defaultEnvironment.SoilEl;
            this.SoilEr = header.SoilEr >= 0 ? Single.Parse(environmentFileRow[header.SoilEr]) : defaultEnvironment.SoilEr;
            this.SoilLeaching = header.SoilLeaching >= 0 ? Single.Parse(environmentFileRow[header.SoilLeaching]) : defaultEnvironment.SoilLeaching;
            this.SoilHumificationRate = header.SoilHumificationRate >= 0 ? Single.Parse(environmentFileRow[header.SoilHumificationRate]) : defaultEnvironment.SoilHumificationRate;
            this.SoilOrganicC = header.SoilOrganicC >= 0 ? Single.Parse(environmentFileRow[header.SoilOrganicC]) : defaultEnvironment.SoilOrganicC;
            this.SoilOrganicDecompositionRate = header.SoilOrganicDecompositionRate >= 0 ? Single.Parse(environmentFileRow[header.SoilOrganicDecompositionRate]) : defaultEnvironment.SoilOrganicDecompositionRate;
            this.SoilOrganicN = header.SoilOrganicN >= 0 ? Single.Parse(environmentFileRow[header.SoilOrganicN]) : defaultEnvironment.SoilOrganicN;
            this.SoilClay = header.SoilClayPercentage >= 0 ? Single.Parse(environmentFileRow[header.SoilClayPercentage]) : defaultEnvironment.SoilClay;
            this.SoilSand = header.SoilSandPercentage >= 0 ? Single.Parse(environmentFileRow[header.SoilSandPercentage]) : defaultEnvironment.SoilSand;
            this.SoilSilt = header.SoilSiltPercentage >= 0 ? Single.Parse(environmentFileRow[header.SoilSiltPercentage]) : defaultEnvironment.SoilSilt;
            this.SoilQh = header.SoilQh >= 0 ? Single.Parse(environmentFileRow[header.SoilQh]) : defaultEnvironment.SoilQh;
            this.SoilYoungLabileC = header.SoilYoungLabileC >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungLabileC]) : defaultEnvironment.SoilYoungLabileC;
            this.SoilYoungLabileDecompositionRate = header.SoilYoungLabileDecompositionRate >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungLabileDecompositionRate]) : defaultEnvironment.SoilYoungLabileDecompositionRate;
            this.SoilYoungLabileN = header.SoilYoungLabileN >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungLabileN]) : defaultEnvironment.SoilYoungLabileN;
            this.SoilYoungRefractoryC = header.SoilYoungRefractoryC >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungRefractoryC]) : defaultEnvironment.SoilYoungRefractoryC;
            this.SoilYoungRefractoryDecompositionRate = header.SoilYoungRefractoryDecompositionRate >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungRefractoryDecompositionRate]) : defaultEnvironment.SoilYoungRefractoryDecompositionRate;
            this.SoilYoungRefractoryN = header.SoilYoungRefractoryN >= 0 ? Single.Parse(environmentFileRow[header.SoilYoungRefractoryN]) : defaultEnvironment.SoilYoungRefractoryN;

            this.SpeciesTableName = header.SpeciesTableName >= 0 ? environmentFileRow[header.SpeciesTableName] : defaultEnvironment.SpeciesTableName;

            this.SoilQb = defaultEnvironment.SoilQb;
            this.UseDynamicAvailableNitrogen = defaultEnvironment.UseDynamicAvailableNitrogen;
        }

        public ResourceUnitEnvironment(ProjectFile.World world)
        {
            this.ClimateID = world.Climate.DefaultDatabaseTable ?? String.Empty;
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
            this.SoilQb = world.DefaultSoil.SoilMicrobeCarbonNitrogenRatio;
            this.UseDynamicAvailableNitrogen = world.DefaultSoil.UseDynamicAvailableNitrogen;

            // default soil parameters which can be overridden in environment file
            this.AnnualNitrogenDeposition = world.DefaultSoil.NitrogenDeposition;
            this.SoilEl = world.DefaultSoil.MicrobialLabileEfficiency;
            this.SoilEr = world.DefaultSoil.MicrobialRefractoryEfficiency;
            this.SoilLeaching = world.DefaultSoil.NitrogenLeachingFraction;

            // default soil parameters specified in <site> rather than in <defaultSoil>
            // parameters used by resource unit soil
            this.SoilAvailableNitrogen = world.DefaultSoil.AvailableNitrogen;
            this.SoilDepthInCM = world.DefaultSoil.SoilDepth;
            this.SoilHumificationRate = world.DefaultSoil.SoilHumificationRate;
            this.SoilOrganicC = world.DefaultSoil.SoilOrganicMatterCarbon;
            this.SoilOrganicDecompositionRate = world.DefaultSoil.SoilOrganicMatterDecompositionRate;
            this.SoilOrganicN = world.DefaultSoil.SoilOrganicMatterNitrogen;
            this.SoilQh = world.DefaultSoil.SoilOrganicMatterCarbonNitrogenRatio;
            this.SoilYoungLabileC = world.DefaultSoil.YoungLabileCarbon;
            this.SoilYoungLabileDecompositionRate = world.DefaultSoil.YoungLabileDecompositionRate; // also in species table
            this.SoilYoungLabileN = world.DefaultSoil.YoungLabileNitrogen;
            this.SoilYoungRefractoryC = world.DefaultSoil.YoungRefractoryCarbon;
            this.SoilYoungRefractoryDecompositionRate = world.DefaultSoil.YoungRefractoryDecompositionRate; // also in species table
            this.SoilYoungRefractoryN = world.DefaultSoil.YoungRefractoryNitrogen;
            // parameters used by resource unit water cycle
            this.SoilSand = world.DefaultSoil.PercentSand;
            this.SoilSilt = world.DefaultSoil.PercentSilt;
            this.SoilClay = world.DefaultSoil.PercentClay;

            this.SpeciesTableName = world.Species.DatabaseTable ?? String.Empty;
        }

        public string GetCentroidKey()
        {
            return (int)this.GisCenterX + "_" + (int)this.GisCenterY;
        }
    }
}
