﻿using iLand.Simulation;
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

        public ResourceUnitWrapper()
        {
            ResourceUnit = null;
        }

        public ResourceUnitWrapper(ResourceUnit resourceUnit)
        {
            ResourceUnit = resourceUnit;
        }

        public override ReadOnlyCollection<string> GetVariableNames()
        {
            return VariableNames;
        }

        public override double GetValue(Model model, int variableIndex)
        {
            Debug.Assert(ResourceUnit != null);

            switch (variableIndex - BaseVariableNames.Count)
            {
                case 0: return ResourceUnit.EnvironmentID; // id from grid
                case 1: return ResourceUnit.EffectiveAreaPerWla;
                case 2: return ResourceUnit.Soil.PlantAvailableNitrogen;
                case 3: return ResourceUnit.WaterCycle.SoilDepth;
                case 4: return ResourceUnit.StockedArea;
                case 5: return ResourceUnit.StockableArea;
                case 6: return ResourceUnit.Statistics.TreesPerHectare;
                case 7: return ResourceUnit.Statistics.StemVolume;
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
                        return ResourceUnit.Snags.StandingAndDebrisCarbon;
                    }
                    else
                    {
                        return 0.0;
                    }
                case 20: return ResourceUnit.GridIndex; // numeric index
                case 21: return ResourceUnit.Climate.MeanAnnualTemperature; // mean temperature
                case 22:
                    {
                        double psum = 0;
                        for (int i = 0; i < 12; ++i)
                        {
                            psum += ResourceUnit.Climate.PrecipitationByMonth[i];
                        }
                        return psum;
                    }
                case 23: 
                    return ResourceUnit.Climate.TotalAnnualRadiation;
                default:
                    return base.GetValue(model, variableIndex);
            }
        }
    }
}