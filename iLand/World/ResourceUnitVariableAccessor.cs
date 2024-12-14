// C++/tool/{ expressionwrapper.h, expressionwrapper.cpp }
using iLand.Simulation;
using iLand.Tool;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace iLand.World
{
    internal class ResourceUnitVariableAccessor : ExpressionVariableAccessor
    {
        private static readonly ReadOnlyCollection<string> VariableNames;

        public ResourceUnit? ResourceUnit { get; set; }

        static ResourceUnitVariableAccessor()
        {
            ResourceUnitVariableAccessor.VariableNames = new List<string>(ExpressionVariableAccessor.BaseVariableNames) 
            { 
                "id", "totalEffectiveArea", 
                "nitrogenAvailable", "soilDepth", "stockedArea", "stockableArea",
                "count", "volume", "avgDbh", "avgHeight", "basalArea", 
                "leafAreaIndex", "aging", "cohortCount", "saplingCount", "saplingAge",
                "canopyConductance", 
                "soilC", "soilN", 
                "snagC", "index", "meanTemp", "annualPrecip", "annualRad",
                "LAIsaplings", "waterHoldingCapacity"
            }.AsReadOnly();
        }

        public ResourceUnitVariableAccessor(SimulationState simulationState)
            : base(simulationState, null)
        {
            this.ResourceUnit = null;
        }

        public override ReadOnlyCollection<string> GetVariableNames()
        {
            return ResourceUnitVariableAccessor.VariableNames;
        }

        public override float GetValue(int variableIndex)
        {
            Debug.Assert(this.ResourceUnit != null);

            switch (variableIndex - ExpressionVariableAccessor.BaseVariableNames.Count)
            {
                // ResourceUnitVariableAccessor.VariableNames[variableIndex]
                case 0: 
                    return this.ResourceUnit.ID; // id from grid
                case 1: 
                    return this.ResourceUnit.Trees.PhotosyntheticallyActiveAreaPerLightWeightedLeafArea;
                case 2:
                    if (this.ResourceUnit.Soil != null)
                    {
                        return this.ResourceUnit.Soil.PlantAvailableNitrogen;
                    }
                    return Single.NaN;
                case 3:
                    if (this.ResourceUnit.WaterCycle.SoilWaterRetention != null)
                    {
                        return this.ResourceUnit.WaterCycle.SoilWaterRetention.SoilPlantAccessibleDepthInMM;
                    }
                    return Single.NaN;
                case 4: 
                    return this.ResourceUnit.AreaWithTreesInM2;
                case 5: 
                    return this.ResourceUnit.AreaInLandscapeInM2;
                case 6: 
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.TreesPerHa;
                case 7: 
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.StemVolumeInM3PerHa;
                case 8: 
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.AverageDbhInCm;
                case 9: 
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.AverageHeightInM;
                case 10: 
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.BasalAreaInM2PerHa;
                case 11:
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.GetTotalCarbon();
                case 12:
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.LeafAreaIndex;
                case 13:
                    return this.ResourceUnit.Trees.AverageLeafAreaWeightedAgingFactor;
                case 14: 
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.SaplingCohortsPerHa;
                case 15: 
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.SaplingsPerHa;
                case 16: 
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.SaplingMeanAgeInYears;
                case 17:
                    return this.ResourceUnit.WaterCycle.CanopyConductance;
                // soil C + soil N
                case 18:
                    if (this.ResourceUnit.Soil != null)
                    {
                        return this.ResourceUnit.Soil.YoungLabile.C + this.ResourceUnit.Soil.YoungRefractory.C + this.ResourceUnit.Soil.OrganicMatter.C;
                    }
                    return 0.0F;
                case 19:
                    if (this.ResourceUnit.Soil != null)
                    {
                        return this.ResourceUnit.Soil.YoungLabile.N + this.ResourceUnit.Soil.YoungRefractory.N + this.ResourceUnit.Soil.OrganicMatter.N;
                    }
                    return 0.0F;
                // snags
                case 20:
                    if (this.ResourceUnit.Snags != null)
                    {
                        return this.ResourceUnit.Snags.StandingAndDebrisCarbon;
                    }
                    return 0.0F;
                case 21: 
                    return this.ResourceUnit.ResourceUnitGridIndex; // numeric index
                case 22: 
                    return this.ResourceUnit.Weather.MeanAnnualTemperature; // mean temperature
                case 23:
                    float totalAnnualPrecipitation = 0;
                    for (int monthIndex = 0; monthIndex < 12; ++monthIndex)
                    {
                        totalAnnualPrecipitation += this.ResourceUnit.Weather.PrecipitationByMonth[monthIndex];
                    }
                    return totalAnnualPrecipitation;
                case 24: 
                    return this.ResourceUnit.Weather.TotalAnnualRadiation;
                case 25:
                    return this.ResourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies.SaplingLeafAreaIndex;
                case 26:
                    return this.ResourceUnit.WaterCycle.FieldCapacityInMM;
                default:
                    return base.GetValue(variableIndex);
            }
        }
    }
}
