using iLand.core;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.tools
{
    internal class RUWrapper : ExpressionWrapper
    {
        private static readonly List<string> ruVarList;

        public ResourceUnit ResourceUnit { get; set; }

        static RUWrapper()
        {
            ruVarList = new List<string>(BaseVarList);
            ruVarList.AddRange(new string[] { "id", "totalEffectiveArea", "nitrogenAvailable", "soilDepth", "stockedArea", "stockableArea",
                         "count", "volume", "avgDbh", "avgHeight", "basalArea", "leafAreaIndex", "aging", "cohortCount", "saplingCount", "saplingAge",
                         "canopyConductance", "soilC", "soilN", "snagC", "index", "meanTemp", "annualPrecip", "annualRad" });
        }

        public RUWrapper()
        {
            ResourceUnit = null;
        }

        public RUWrapper(ResourceUnit resourceUnit)
        {
            ResourceUnit = resourceUnit;
        }

        public override List<string> GetVariablesList()
        {
            return ruVarList;
        }

        public override double Value(int variableIndex)
        {
            Debug.Assert(ResourceUnit != null);

            switch (variableIndex - BaseVarList.Count)
            {
                case 0: return ResourceUnit.ID; // id from grid
                case 1: return ResourceUnit.mEffectiveArea_perWLA;
                case 2: return ResourceUnit.Variables.NitrogenAvailable;
                case 3: return ResourceUnit.WaterCycle.SoilDepth;
                case 4: return ResourceUnit.StockedArea;
                case 5: return ResourceUnit.StockableArea;
                case 6: return ResourceUnit.Statistics.Count;
                case 7: return ResourceUnit.Statistics.Volume;
                case 8: return ResourceUnit.Statistics.AverageDbh;
                case 9: return ResourceUnit.Statistics.AverageHeight;
                case 10: return ResourceUnit.Statistics.BasalArea;
                case 11: return ResourceUnit.Statistics.LeafAreaIndex;
                case 12: return ResourceUnit.AverageAging;
                case 13: return ResourceUnit.Statistics.CohortCount;
                case 14: return ResourceUnit.Statistics.SaplingCount;
                case 15: return ResourceUnit.Statistics.MeanSaplingAge;
                case 16: return ResourceUnit.WaterCycle.CanopyConductance;
                // soil C + soil N
                case 17:
                    if (ResourceUnit.Soil != null)
                    {
                        return ResourceUnit.Soil.YoungLabile.C + ResourceUnit.Soil.YoungRefractory.C + ResourceUnit.Soil.OrganicMatter.C;
                    }
                    else
                    {
                        return 0.0;
                    }
                case 18:
                    if (ResourceUnit.Soil != null)
                    {
                        return ResourceUnit.Soil.YoungLabile.N + ResourceUnit.Soil.YoungRefractory.N + ResourceUnit.Soil.OrganicMatter.N;
                    }
                    else
                    {
                        return 0.0;
                    }
                // snags
                case 19:
                    if (ResourceUnit.Snags != null)
                    {
                        return ResourceUnit.Snags.TotalCarbon;
                    }
                    else
                    {
                        return 0.0;
                    }
                case 20: return ResourceUnit.Index; // numeric index
                case 21: return ResourceUnit.Climate.MeanAnnualTemperature; // mean temperature
                case 22:
                    {
                        double psum = 0;
                        for (int i = 0; i < 12; ++i)
                        {
                            psum += ResourceUnit.Climate.PrecipitationMonth[i];
                        }
                        return psum;
                    }
                case 23: 
                    return ResourceUnit.Climate.TotalRadiation;
                default:
                    return base.Value(variableIndex);
            }
        }
    }
}
