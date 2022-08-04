namespace iLand.Input
{
    public class ResourceUnitHeaderCsv
    {
        public int AnnualNitrogenDeposition { get; private init; }
        public int CenterX { get; private init; }
        public int CenterY { get; private init; }
        public int ResourceUnitID { get; private init; }

        public int SnagBranchRootCarbon { get; private init; }
        public int SnagBranchRootCNRatio { get; private init; }
        public int SnagBranchRootDecompositionRate { get; private init; }
        public int SnagStemCarbon { get; private init; }
        public int SnagStemCNRatio { get; private init; }
        public int SnagsPerResourceUnit { get; private init; }
        public int SnagStemDecompositionRate { get; private init; }
        public int SnagHalfLife { get; private init; }

        public int SpeciesTableName { get; private init; }

        public int SoilPlantAccessibleDepthInCm { get; private init; }

        public int SoilSandPercentage { get; private init; }
        public int SoilSiltPercentage { get; private init; }
        public int SoilClayPercentage { get; private init; }

        public int SoilThetaR { get; private init; }
        public int SoilThetaS { get; private init; }
        public int SoilVanGenuchtenAlpha { get; private init; }
        public int SoilVanGenuchtenN { get; private init; }

        public int SoilAvailableNitrogen { get; private init; }
        public int SoilEr { get; private init; }
        public int SoilEl { get; private init; }
        public int SoilLeaching { get; private init; }
        public int SoilHumificationRate { get; private init; }
        public int SoilOrganicMatterC { get; private init; }
        public int SoilOrganicMatterDecompositionRate { get; private init; }
        public int SoilOrganicMatterN { get; private init; }
        public int SoilQh { get; private init; }
        public int SoilYoungLabileC { get; private init; }
        public int SoilYoungLabileDecompositionRate { get; private init; }
        public int SoilYoungLabileN { get; private init; }
        public int SoilYoungRefractoryC { get; private init; }
        public int SoilYoungRefractoryDecompositionRate { get; private init; }
        public int SoilYoungRefractoryN { get; private init; }

        public int WeatherID { get; private init; }

        public ResourceUnitHeaderCsv(CsvFile environmentFile)
        {
            this.AnnualNitrogenDeposition = environmentFile.GetColumnIndex("soilAnnualNitrogenDeposition");
            this.CenterX = environmentFile.GetColumnIndex("centerX");
            this.CenterY = environmentFile.GetColumnIndex("centerY");
            this.ResourceUnitID = environmentFile.GetColumnIndex("id");
            this.SnagBranchRootCarbon = environmentFile.GetColumnIndex("snagBranchRootC");
            this.SnagBranchRootCNRatio = environmentFile.GetColumnIndex("snagBranchRootCN");
            this.SnagHalfLife = environmentFile.GetColumnIndex("snagHalfLife");
            this.SnagsPerResourceUnit = environmentFile.GetColumnIndex("snagCount");
            this.SnagStemCarbon = environmentFile.GetColumnIndex("snagCarbon");
            this.SnagStemCNRatio = environmentFile.GetColumnIndex("snagCNRatio");
            this.SnagStemDecompositionRate = environmentFile.GetColumnIndex("snagDecompositionRate");
            this.SoilAvailableNitrogen = environmentFile.GetColumnIndex("soilAvailableNitrogen");
            this.SoilClayPercentage = environmentFile.GetColumnIndex("soilClayPercent");
            this.SoilPlantAccessibleDepthInCm = environmentFile.GetColumnIndex("soilPlantAccessibleDepth");
            this.SoilEl = environmentFile.GetColumnIndex("soilEl");
            this.SoilEr = environmentFile.GetColumnIndex("soilEr");
            this.SoilHumificationRate = environmentFile.GetColumnIndex("soilHumificationRate");
            this.SoilLeaching = environmentFile.GetColumnIndex("soilLeaching");
            this.SoilOrganicMatterC = environmentFile.GetColumnIndex("soilOrganicC");
            this.SoilOrganicMatterDecompositionRate = environmentFile.GetColumnIndex("soilOrganicDecompositionRate");
            this.SoilOrganicMatterN = environmentFile.GetColumnIndex("soilOrganicN");
            this.SoilQh = environmentFile.GetColumnIndex("soilQh");
            this.SoilSandPercentage = environmentFile.GetColumnIndex("soilSandPercent");
            this.SoilSiltPercentage = environmentFile.GetColumnIndex("soilSiltPercent");
            this.SoilThetaR = environmentFile.GetColumnIndex("soilThetaR");
            this.SoilThetaS = environmentFile.GetColumnIndex("soilThetaS");
            this.SoilVanGenuchtenAlpha = environmentFile.GetColumnIndex("soilVanGenuchtenAlpha");
            this.SoilVanGenuchtenN = environmentFile.GetColumnIndex("soilVanGenuchtenN");
            this.SoilYoungLabileC = environmentFile.GetColumnIndex("soilYoungLabileC");
            this.SoilYoungLabileDecompositionRate = environmentFile.GetColumnIndex("soilYoungLabileDecompositionRate");
            this.SoilYoungLabileN = environmentFile.GetColumnIndex("soilYoungLabileN");
            this.SoilYoungRefractoryC = environmentFile.GetColumnIndex("soilYoungRefractoryC");
            this.SoilYoungRefractoryDecompositionRate = environmentFile.GetColumnIndex("soilYoungRefractoryDecompositionRate");
            this.SoilYoungRefractoryN = environmentFile.GetColumnIndex("soilYoungRefractoryN");
            this.SpeciesTableName = environmentFile.GetColumnIndex("speciesTable");
            this.WeatherID = environmentFile.GetColumnIndex("weatherID");
        }
    }
}
