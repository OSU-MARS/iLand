using Apache.Arrow;
using System.Linq;

namespace iLand.Input
{
    public class ResourceUnitArrowBatch : ArrowBatch
    {
        public FloatArray? AnnualNitrogenDeposition { get; private init; }
        public StringArray? WeatherID { get; private init; }
        public FloatArray CenterX { get; private init; }
        public FloatArray CenterY { get; private init; }
        public UInt32Array ResourceUnitID { get; private init; }

        public FloatArray? SnagBranchRootCarbon { get; private init; }
        public FloatArray? SnagBranchRootCNRatio { get; private init; }
        public FloatArray? SnagOtherWoodAbovegroundFraction { get; private init; }
        public FloatArray? SnagStemCarbon { get; private init; }
        public FloatArray? SnagStemCNRatio { get; private init; }
        public FloatArray? SnagStemDecompositionRate { get; private init; }
        public FloatArray? SnagHalfLife { get; private init; }
        public FloatArray? SnagsPerResourceUnit { get; private init; }

        public FloatArray? SoilPlantAccessibleDepthInCm { get; private init; }

        public FloatArray? SoilThetaR { get; private init; }
        public FloatArray? SoilThetaS { get; private init; }
        public FloatArray? SoilVanGenuchtenAlpha { get; private init; }
        public FloatArray? SoilVanGenuchtenN { get; private init; }

        public FloatArray? SoilClayPercentage { get; private init; }
        public FloatArray? SoilSandPercentage { get; private init; }
        public FloatArray? SoilSiltPercentage { get; private init; }

        public FloatArray? SoilAvailableNitrogen { get; private init; }
        public FloatArray? SoilEl { get; private init; }
        public FloatArray? SoilEr { get; private init; }
        public FloatArray? SoilLeaching { get; private init; }
        public FloatArray? SoilHumificationRate { get; private init; }
        public FloatArray? SoilOrganicMatterC { get; private init; }
        public FloatArray? SoilOrganicMatterDecompositionRate { get; private init; }
        public FloatArray? SoilOrganicMatterN { get; private init; }
        public FloatArray? SoilQh { get; private init; }
        public FloatArray? SoilYoungLabileAbovegroundFraction { get; private init; }
        public FloatArray? SoilYoungLabileC { get; private init; }
        public FloatArray? SoilYoungLabileDecompositionRate { get; private init; }
        public FloatArray? SoilYoungLabileN { get; private init; }
        public FloatArray? SoilYoungRefractoryAbovegroundFraction { get; private init; }
        public FloatArray? SoilYoungRefractoryC { get; private init; }
        public FloatArray? SoilYoungRefractoryDecompositionRate { get; private init; }
        public FloatArray? SoilYoungRefractoryN { get; private init; }

        public StringArray? SpeciesTableName { get; private init; }

        public ResourceUnitArrowBatch(RecordBatch arrowBatch)
        {
            IArrowArray[] fields = arrowBatch.Arrays.ToArray();
            Schema schema = arrowBatch.Schema;

            this.AnnualNitrogenDeposition = ArrowBatch.MaybeGetArray<FloatArray>("soilAnnualNitrogenDeposition", schema, fields);
            this.CenterX = ArrowBatch.GetArray<FloatArray>("centerX", schema, fields);
            this.CenterY = ArrowBatch.GetArray<FloatArray>("centerY", schema, fields);
            this.ResourceUnitID = ArrowBatch.GetArray<UInt32Array>("id", schema, fields);
            this.SnagBranchRootCarbon = ArrowBatch.MaybeGetArray<FloatArray>("snagBranchRootC", schema, fields);
            this.SnagBranchRootCNRatio = ArrowBatch.MaybeGetArray<FloatArray>("snagBranchRootCN", schema, fields);
            this.SnagHalfLife = ArrowBatch.MaybeGetArray<FloatArray>("snagHalfLife", schema, fields);
            this.SnagOtherWoodAbovegroundFraction = ArrowBatch.MaybeGetArray<FloatArray>("snagOtherWoodAbovegroundFraction", schema, fields);
            this.SnagsPerResourceUnit = ArrowBatch.MaybeGetArray<FloatArray>("snagCount", schema, fields);
            this.SnagStemCarbon = ArrowBatch.MaybeGetArray<FloatArray>("snagCarbon", schema, fields);
            this.SnagStemCNRatio = ArrowBatch.MaybeGetArray<FloatArray>("snagCNRatio", schema, fields);
            this.SnagStemDecompositionRate = ArrowBatch.MaybeGetArray<FloatArray>("snagDecompositionRate", schema, fields);
            this.SoilAvailableNitrogen = ArrowBatch.MaybeGetArray<FloatArray>("soilAvailableNitrogen", schema, fields);
            this.SoilClayPercentage = ArrowBatch.MaybeGetArray<FloatArray>("soilClayPercent", schema, fields);
            this.SoilPlantAccessibleDepthInCm = ArrowBatch.MaybeGetArray<FloatArray>("soilPlantAccessibleDepth", schema, fields);
            this.SoilEl = ArrowBatch.MaybeGetArray<FloatArray>("soilEl", schema, fields);
            this.SoilEr = ArrowBatch.MaybeGetArray<FloatArray>("soilEr", schema, fields);
            this.SoilHumificationRate = ArrowBatch.MaybeGetArray<FloatArray>("soilHumificationRate", schema, fields);
            this.SoilLeaching = ArrowBatch.MaybeGetArray<FloatArray>("soilLeaching", schema, fields);
            this.SoilOrganicMatterC = ArrowBatch.MaybeGetArray<FloatArray>("soilOrganicC", schema, fields);
            this.SoilOrganicMatterDecompositionRate = ArrowBatch.MaybeGetArray<FloatArray>("soilOrganicDecompositionRate", schema, fields);
            this.SoilOrganicMatterN = ArrowBatch.MaybeGetArray<FloatArray>("soilOrganicN", schema, fields);
            this.SoilQh = ArrowBatch.MaybeGetArray<FloatArray>("soilQh", schema, fields);
            this.SoilSandPercentage = ArrowBatch.MaybeGetArray<FloatArray>("soilSandPercent", schema, fields);
            this.SoilSiltPercentage = ArrowBatch.MaybeGetArray<FloatArray>("soilSiltPercent", schema, fields);
            this.SoilThetaR = ArrowBatch.MaybeGetArray<FloatArray>("soilThetaR", schema, fields);
            this.SoilThetaS = ArrowBatch.MaybeGetArray<FloatArray>("soilThetaS", schema, fields);
            this.SoilVanGenuchtenAlpha = ArrowBatch.MaybeGetArray<FloatArray>("soilVanGenuchtenAlpha", schema, fields);
            this.SoilVanGenuchtenN = ArrowBatch.MaybeGetArray<FloatArray>("soilVanGenuchtenN", schema, fields);
            this.SoilYoungLabileAbovegroundFraction = ArrowBatch.MaybeGetArray<FloatArray>("soilYoungLabileAbovegroundFraction", schema, fields);
            this.SoilYoungLabileC = ArrowBatch.MaybeGetArray<FloatArray>("soilYoungLabileC", schema, fields);
            this.SoilYoungLabileDecompositionRate = ArrowBatch.MaybeGetArray<FloatArray>("soilYoungLabileDecompositionRate", schema, fields);
            this.SoilYoungLabileN = ArrowBatch.MaybeGetArray<FloatArray>("soilYoungLabileN", schema, fields);
            this.SoilYoungRefractoryAbovegroundFraction = ArrowBatch.MaybeGetArray<FloatArray>("soilYoungRefractoryAbovegroundFraction", schema, fields);
            this.SoilYoungRefractoryC = ArrowBatch.MaybeGetArray<FloatArray>("soilYoungRefractoryC", schema, fields);
            this.SoilYoungRefractoryDecompositionRate = ArrowBatch.MaybeGetArray<FloatArray>("soilYoungRefractoryDecompositionRate", schema, fields);
            this.SoilYoungRefractoryN = ArrowBatch.MaybeGetArray<FloatArray>("soilYoungRefractoryN", schema, fields);
            this.SpeciesTableName = ArrowBatch.MaybeGetArray<StringArray>("speciesTable", schema, fields);
            this.WeatherID = ArrowBatch.GetArray<StringArray>("weatherID", schema, fields);
        }
    }
}
