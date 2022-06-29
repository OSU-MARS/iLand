using System;

namespace iLand.Input
{
    public class ResourceUnitHeader
    {
        public int AnnualNitrogenDeposition { get; private init; }
        public int ClimateID { get; private init; }
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

        public int SoilAvailableNitrogen { get; private init; }
        public int SoilDepthInCM { get; private init; } // cm
        public int SoilEr { get; private init; }
        public int SoilEl { get; private init; }
        public int SoilLeaching { get; private init; }
        public int SoilHumificationRate { get; private init; }
        public int SoilOrganicC { get; private init; }
        public int SoilOrganicDecompositionRate { get; private init; }
        public int SoilOrganicN { get; private init; }
        public int SoilQh { get; private init; }
        public int SoilSandPercentage { get; private init; }
        public int SoilSiltPercentage { get; private init; }
        public int SoilClayPercentage { get; private init; }
        public int SoilYoungLabileC { get; private init; }
        public int SoilYoungLabileDecompositionRate { get; private init; }
        public int SoilYoungLabileN { get; private init; }
        public int SoilYoungRefractoryC { get; private init; }
        public int SoilYoungRefractoryDecompositionRate { get; private init; }
        public int SoilYoungRefractoryN { get; private init; }

        public int CenterX { get; private init; }
        public int CenterY { get; private init; }

        public ResourceUnitHeader(CsvFile environmentFile)
        {
            this.AnnualNitrogenDeposition = -1;
            this.ClimateID = -1;
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

            this.SoilAvailableNitrogen = -1;
            this.SoilDepthInCM = -1; // cm
            this.SoilEr = -1;
            this.SoilEl = -1;
            this.SoilLeaching = -1;
            this.SoilHumificationRate = -1;
            this.SoilOrganicC = -1;
            this.SoilOrganicDecompositionRate = -1;
            this.SoilOrganicN = -1;
            this.SoilQh = -1;
            this.SoilSandPercentage = -1;
            this.SoilSiltPercentage = -1;
            this.SoilClayPercentage = -1;
            this.SoilYoungLabileC = -1;
            this.SoilYoungLabileDecompositionRate = -1;
            this.SoilYoungLabileN = -1;
            this.SoilYoungRefractoryC = -1;
            this.SoilYoungRefractoryDecompositionRate = -1;
            this.SoilYoungRefractoryN = -1;

            this.CenterX = -1;
            this.CenterY = -1;

            for (int columnIndex = 0; columnIndex < environmentFile.Columns.Count; ++columnIndex)
            {
                switch (environmentFile.Columns[columnIndex])
                {
                    case Constant.Setting.ID:
                        this.ResourceUnitID = columnIndex;
                        break;
                    case Constant.Setting.SpeciesTable:
                        this.SpeciesTableName = columnIndex;
                        break;
                    case Constant.Setting.CenterX:
                        this.CenterX = columnIndex;
                        break;
                    case Constant.Setting.CenterY:
                        this.CenterY = columnIndex;
                        break;
                    case Constant.Setting.Climate.Name:
                        this.ClimateID = columnIndex;
                        break;
                    case Constant.Setting.Snag.OtherC:
                        this.SnagBranchRootCarbon = columnIndex;
                        break;
                    case Constant.Setting.Snag.OtherCN:
                        this.SnagBranchRootCNRatio = columnIndex;
                        break;
                    case Constant.Setting.Snag.StandingWoodyCarbon:
                        this.SnagStemCarbon = columnIndex;
                        break;
                    case Constant.Setting.Snag.StandingWoodyCNRatio:
                        this.SnagStemCNRatio = columnIndex;
                        break;
                    case Constant.Setting.Snag.StandingWoodyDecompositionRate:
                        this.SnagStemDecompositionRate = columnIndex;
                        break;
                    case Constant.Setting.Snag.StandingWoodyHalfLife:
                        this.SnagHalfLife = columnIndex;
                        break;
                    case Constant.Setting.Snag.StandingWoodyCount:
                        this.SnagsPerResourceUnit = columnIndex;
                        break;
                    case Constant.Setting.Soil.AnnualNitrogenDeposition:
                        this.AnnualNitrogenDeposition = columnIndex;
                        break;
                    case Constant.Setting.Soil.AvailableNitrogen:
                        this.SoilAvailableNitrogen = columnIndex;
                        break;
                    case Constant.Setting.Soil.Depth:
                        this.SoilDepthInCM = columnIndex;
                        break;
                    case Constant.Setting.Soil.El:
                        this.SoilEl = columnIndex;
                        break;
                    case Constant.Setting.Soil.Er:
                        this.SoilEr = columnIndex;
                        break;
                    case Constant.Setting.Soil.Leaching:
                        this.SoilLeaching = columnIndex;
                        break;
                    case Constant.Setting.Soil.HumificationRate:
                        this.SoilHumificationRate = columnIndex;
                        break;
                    case Constant.Setting.Soil.OrganicMatterC:
                        this.SoilOrganicC = columnIndex;
                        break;
                    case Constant.Setting.Soil.OrganicMatterDecompositionRate:
                        this.SoilOrganicDecompositionRate = columnIndex;
                        break;
                    case Constant.Setting.Soil.OrganincMatterN:
                        this.SoilOrganicN = columnIndex;
                        break;
                    case Constant.Setting.Soil.PercentClay:
                        this.SoilClayPercentage = columnIndex;
                        break;
                    case Constant.Setting.Soil.PercentSand:
                        this.SoilSandPercentage = columnIndex;
                        break;
                    case Constant.Setting.Soil.PercentSilt:
                        this.SoilSiltPercentage = columnIndex;
                        break;
                    case Constant.Setting.Soil.Qh:
                        this.SoilQh = columnIndex;
                        break;
                    case Constant.Setting.Soil.YoungLabileC:
                        this.SoilYoungLabileC = columnIndex;
                        break;
                    case Constant.Setting.Soil.YoungLabileDecompositionRate:
                        this.SoilYoungLabileDecompositionRate = columnIndex;
                        break;
                    case Constant.Setting.Soil.YoungLabileN:
                        this.SoilYoungLabileN = columnIndex;
                        break;
                    case Constant.Setting.Soil.YoungRefractoryC:
                        this.SoilYoungRefractoryC = columnIndex;
                        break;
                    case Constant.Setting.Soil.YoungRefractoryDecompositionRate:
                        this.SoilYoungRefractoryDecompositionRate = columnIndex;
                        break;
                    case Constant.Setting.Soil.YoungRefractoryN:
                        this.SoilYoungRefractoryN = columnIndex;
                        break;
                    default:
                        throw new NotSupportedException("Unhandled environment column '" + environmentFile.Columns[columnIndex] + "'.");
                }
            }
        }
    }
}
