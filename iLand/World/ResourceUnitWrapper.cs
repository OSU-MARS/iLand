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

        public ResourceUnit ResourceUnit { get; set; }

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

            switch (variableIndex - BaseVariableNames.Count)
            {
                case 0: return this.ResourceUnit.EnvironmentID; // id from grid
                case 1: return this.ResourceUnit.EffectiveAreaPerWla;
                case 2: return this.ResourceUnit.Soil.PlantAvailableNitrogen;
                case 3: return this.ResourceUnit.WaterCycle.SoilDepth;
                case 4: return this.ResourceUnit.StockedArea;
                case 5: return this.ResourceUnit.StockableArea;
                case 6: return this.ResourceUnit.Statistics.TreesPerHectare;
                case 7: return this.ResourceUnit.Statistics.StemVolume;
                case 8: return this.ResourceUnit.Statistics.AverageDbh;
                case 9: return this.ResourceUnit.Statistics.AverageHeight;
                case 10: return this.ResourceUnit.Statistics.BasalArea;
                case 11: return this.ResourceUnit.Statistics.LeafAreaIndex;
                case 12: return this.ResourceUnit.AverageAging;
                case 13: return this.ResourceUnit.Statistics.CohortCount;
                case 14: return this.ResourceUnit.Statistics.SaplingCount;
                case 15: return this.ResourceUnit.Statistics.MeanSaplingAge;
                case 16: return this.ResourceUnit.WaterCycle.CanopyConductance;
                // soil C + soil N
                case 17:
                    if (this.ResourceUnit.Soil != null)
                    {
                        return this.ResourceUnit.Soil.YoungLabile.C + this.ResourceUnit.Soil.YoungRefractory.C + this.ResourceUnit.Soil.OrganicMatter.C;
                    }
                    else
                    {
                        return 0.0;
                    }
                case 18:
                    if (this.ResourceUnit.Soil != null)
                    {
                        return this.ResourceUnit.Soil.YoungLabile.N + this.ResourceUnit.Soil.YoungRefractory.N + this.ResourceUnit.Soil.OrganicMatter.N;
                    }
                    else
                    {
                        return 0.0;
                    }
                // snags
                case 19:
                    if (this.ResourceUnit.Snags != null)
                    {
                        return this.ResourceUnit.Snags.StandingAndDebrisCarbon;
                    }
                    else
                    {
                        return 0.0;
                    }
                case 20: return this.ResourceUnit.GridIndex; // numeric index
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
