using iLand.Simulation;
using iLand.Tools;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace iLand.World
{
    internal class ResourceUnitWrapper : ExpressionWrapper
    {
        private static readonly ReadOnlyCollection<string> VariableNames;

        public ResourceUnit? ResourceUnit { get; set; }

        static ResourceUnitWrapper()
        {
            ResourceUnitWrapper.VariableNames = new List<string>(ExpressionWrapper.BaseVariableNames) 
            { 
                "id", "totalEffectiveArea", "nitrogenAvailable", "soilDepth", "stockedArea", "stockableArea",
                "count", "volume", "avgDbh", "avgHeight", "basalArea", "leafAreaIndex", "aging", "cohortCount", "saplingCount", "saplingAge",
                "canopyConductance", "soilC", "soilN", "snagC", "index", "meanTemp", "annualPrecip", "annualRad"
            }.AsReadOnly();
        }

        public ResourceUnitWrapper(Model model)
            : base(model)
        {
            this.ResourceUnit = null;
        }

        public override ReadOnlyCollection<string> GetVariableNames()
        {
            return ResourceUnitWrapper.VariableNames;
        }

        public override double GetValue(int variableIndex)
        {
            Debug.Assert(this.ResourceUnit != null);

            switch (variableIndex - ExpressionWrapper.BaseVariableNames.Count)
            {
                case 0: return this.ResourceUnit.EnvironmentID; // id from grid
                case 1: return this.ResourceUnit.Trees.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea;
                case 2:
                    if (this.ResourceUnit.Soil != null)
                    {
                        return this.ResourceUnit.Soil.PlantAvailableNitrogen;
                    }
                    return -1.0;
                case 3: return this.ResourceUnit.WaterCycle.SoilDepth;
                case 4: return this.ResourceUnit.AreaWithTrees;
                case 5: return this.ResourceUnit.AreaInLandscape;
                case 6: return this.ResourceUnit.Trees.Statistics.TreesPerHectare[^1];
                case 7: return this.ResourceUnit.Trees.Statistics.StemVolume[^1];
                case 8: return this.ResourceUnit.Trees.Statistics.AverageDbh[^1];
                case 9: return this.ResourceUnit.Trees.Statistics.AverageHeight[^1];
                case 10: return this.ResourceUnit.Trees.Statistics.BasalArea[^1];
                case 11: return this.ResourceUnit.Trees.Statistics.LeafAreaIndex[^1];
                case 12: return this.ResourceUnit.Trees.AverageLeafAreaWeightedAgingFactor;
                case 13: return this.ResourceUnit.Trees.Statistics.CohortCount[^1];
                case 14: return this.ResourceUnit.Trees.Statistics.SaplingCount[^1];
                case 15: return this.ResourceUnit.Trees.Statistics.MeanSaplingAge[^1];
                case 16: return this.ResourceUnit.WaterCycle.CanopyConductance;
                // soil C + soil N
                case 17:
                    if (this.ResourceUnit.Soil != null)
                    {
                        return this.ResourceUnit.Soil.YoungLabile.C + this.ResourceUnit.Soil.YoungRefractory.C + this.ResourceUnit.Soil.OrganicMatter.C;
                    }
                    return 0.0;
                case 18:
                    if (this.ResourceUnit.Soil != null)
                    {
                        return this.ResourceUnit.Soil.YoungLabile.N + this.ResourceUnit.Soil.YoungRefractory.N + this.ResourceUnit.Soil.OrganicMatter.N;
                    }
                    return 0.0;
                // snags
                case 19:
                    if (this.ResourceUnit.Snags != null)
                    {
                        return this.ResourceUnit.Snags.StandingAndDebrisCarbon;
                    }
                    return 0.0;
                case 20: return this.ResourceUnit.ResourceUnitGridIndex; // numeric index
                case 21: return this.ResourceUnit.Climate.MeanAnnualTemperature; // mean temperature
                case 22:
                    {
                        double psum = 0;
                        for (int i = 0; i < 12; ++i)
                        {
                            psum += this.ResourceUnit.Climate.PrecipitationByMonth[i];
                        }
                        return psum;
                    }
                case 23: 
                    return this.ResourceUnit.Climate.TotalAnnualRadiation;
                default:
                    return base.GetValue(variableIndex);
            }
        }
    }
}
