using System;

namespace iLand.Input
{
    public class ResourceUnitHeader
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

        public int SoilDepthInCM { get; private init; } // cm

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

        public ResourceUnitHeader(CsvFile environmentFile)
        {
            this.AnnualNitrogenDeposition = -1;
            this.CenterX = -1;
            this.CenterY = -1;
            this.ResourceUnitID = -1;

            this.SnagBranchRootCarbon = -1;
            this.SnagBranchRootCNRatio = -1;
            this.SnagBranchRootDecompositionRate = -1;
            this.SnagStemCarbon = -1;
            this.SnagStemCNRatio = -1;
            this.SnagsPerResourceUnit = -1;
            this.SnagStemDecompositionRate = -1;
            this.SnagHalfLife = -1;

            this.SpeciesTableName = -1;

            this.SoilDepthInCM = -1; // cm

            this.SoilThetaR = -1;
            this.SoilThetaS = -1;
            this.SoilVanGenuchtenAlpha = -1;
            this.SoilVanGenuchtenN = -1;

            this.SoilSandPercentage = -1;
            this.SoilSiltPercentage = -1;
            this.SoilClayPercentage = -1;

            this.SoilAvailableNitrogen = -1;
            this.SoilEr = -1;
            this.SoilEl = -1;
            this.SoilLeaching = -1;
            this.SoilHumificationRate = -1;
            this.SoilOrganicMatterC = -1;
            this.SoilOrganicMatterDecompositionRate = -1;
            this.SoilOrganicMatterN = -1;
            this.SoilQh = -1;
            this.SoilYoungLabileC = -1;
            this.SoilYoungLabileDecompositionRate = -1;
            this.SoilYoungLabileN = -1;
            this.SoilYoungRefractoryC = -1;
            this.SoilYoungRefractoryDecompositionRate = -1;
            this.SoilYoungRefractoryN = -1;

            this.WeatherID = -1;

            for (int columnIndex = 0; columnIndex < environmentFile.Columns.Count; ++columnIndex)
            {
                switch (environmentFile.Columns[columnIndex])
                {
                    case "id":
                        this.ResourceUnitID = columnIndex;
                        break;
                    case "speciesTable":
                        this.SpeciesTableName = columnIndex;
                        break;
                    case "centerX":
                        this.CenterX = columnIndex;
                        break;
                    case "centerY":
                        this.CenterY = columnIndex;
                        break;
                    case "snagBranchRootC":
                        this.SnagBranchRootCarbon = columnIndex;
                        break;
                    case "snagBranchRootCN":
                        this.SnagBranchRootCNRatio = columnIndex;
                        break;
                    case "snagCarbon":
                        this.SnagStemCarbon = columnIndex;
                        break;
                    case "snagCNRatio":
                        this.SnagStemCNRatio = columnIndex;
                        break;
                    case "snagDecompositionRate":
                        this.SnagStemDecompositionRate = columnIndex;
                        break;
                    case "snagHalfLife":
                        this.SnagHalfLife = columnIndex;
                        break;
                    case "snagCount":
                        this.SnagsPerResourceUnit = columnIndex;
                        break;
                    case "soilAnnualNitrogenDeposition":
                        this.AnnualNitrogenDeposition = columnIndex;
                        break;
                    case "soilAvailableNitrogen":
                        this.SoilAvailableNitrogen = columnIndex;
                        break;
                    case "soilDepth":
                        this.SoilDepthInCM = columnIndex;
                        break;
                    case "soilEl":
                        this.SoilEl = columnIndex;
                        break;
                    case "soilEr":
                        this.SoilEr = columnIndex;
                        break;
                    case "soilLeaching":
                        this.SoilLeaching = columnIndex;
                        break;
                    case "soilHumificationRate":
                        this.SoilHumificationRate = columnIndex;
                        break;
                    case "soilOrganicC":
                        this.SoilOrganicMatterC = columnIndex;
                        break;
                    case "soilOrganicDecompositionRate":
                        this.SoilOrganicMatterDecompositionRate = columnIndex;
                        break;
                    case "soilOrganicN":
                        this.SoilOrganicMatterN = columnIndex;
                        break;
                    case "soilClayPercent":
                        this.SoilClayPercentage = columnIndex;
                        break;
                    case "soilSandPercent":
                        this.SoilSandPercentage = columnIndex;
                        break;
                    case "soilSiltPercent":
                        this.SoilSiltPercentage = columnIndex;
                        break;
                    case "soilQh":
                        this.SoilQh = columnIndex;
                        break;
                    case "soilThetaR":
                        this.SoilThetaR = columnIndex;
                        break;
                    case "soilThetaS":
                        this.SoilThetaS = columnIndex;
                        break;
                    case "soilVanGenuchtenAlpha":
                        this.SoilVanGenuchtenAlpha = columnIndex;
                        break;
                    case "soilVanGenuchtenN":
                        this.SoilVanGenuchtenN = columnIndex;
                        break;
                    case "soilYoungLabileC":
                        this.SoilYoungLabileC = columnIndex;
                        break;
                    case "soilYoungLabileDecompositionRate":
                        this.SoilYoungLabileDecompositionRate = columnIndex;
                        break;
                    case "soilYoungLabileN":
                        this.SoilYoungLabileN = columnIndex;
                        break;
                    case "soilYoungRefractoryC":
                        this.SoilYoungRefractoryC = columnIndex;
                        break;
                    case "soilYoungRefractoryDecompositionRate":
                        this.SoilYoungRefractoryDecompositionRate = columnIndex;
                        break;
                    case "soilYoungRefractoryN":
                        this.SoilYoungRefractoryN = columnIndex;
                        break;
                    case "weatherID":
                        this.WeatherID = columnIndex;
                        break;
                    default:
                        throw new NotSupportedException("Unhandled environment column '" + environmentFile.Columns[columnIndex] + "'.");
                }
            }
        }
    }
}
