﻿using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    public class CarbonAnnualOutput : AnnualOutput
    {
        private readonly Expression yearFilter; // condition for landscape-level output
        private readonly Expression resourceUnitFilter; // condition for resource-unit-level output

        public CarbonAnnualOutput()
        {
            this.yearFilter = new();
            this.resourceUnitFilter = new();

            this.Name = "Carbon and nitrogen pools above and belowground per RU/yr";
            this.TableName = "carbon";
            this.Description = "Carbon and nitrogen pools (C and N) per resource unit / year and/or by landsacpe/year. " +
                               "On resource unit level, the outputs contain aggregated above ground pools (kg/ha) " +
                               "and below ground pools (kg/ha). " + Environment.NewLine +
                               "For landscape level outputs, all variables are scaled to kg/ha stockable area. " +
                               "The area column contains the stockable area (per resource unit / landscape) and can be used to scale to values to the actual value. " + Environment.NewLine +
                               "You can use the 'condition' to control if the output should be created for the current year(see also dynamic stand output)." + Environment.NewLine +
                               "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                               "(leaving 'conditionRU' blank enables details per default).";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(new("area_ha", "total stockable area of the resource unit (ha)", SqliteType.Real));
            this.Columns.Add(new("stem_c", "Stem carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("stem_n", "Stem nitrogen kg/ha", SqliteType.Real));
            this.Columns.Add(new("branch_c", "branches carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("branch_n", "branches nitrogen kg/ha", SqliteType.Real));
            this.Columns.Add(new("foliage_c", "Foliage carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("foliage_n", "Foliage nitrogen kg/ha", SqliteType.Real));
            this.Columns.Add(new("coarseRoot_c", "coarse root carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("coarseRoot_n", "coarse root nitrogen kg/ha", SqliteType.Real));
            this.Columns.Add(new("fineRoot_c", "fine root carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("fineRoot_n", "fine root nitrogen kg/ha", SqliteType.Real));
            this.Columns.Add(new("regeneration_c", "total carbon in regeneration layer (h<4m) kg/ha", SqliteType.Real));
            this.Columns.Add(new("regeneration_n", "total nitrogen in regeneration layer (h<4m) kg/ha", SqliteType.Real));
            this.Columns.Add(new("snags_c", "standing dead wood carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("snags_n", "standing dead wood nitrogen kg/ha", SqliteType.Real));
            this.Columns.Add(new("snagsOther_c", "branches and coarse roots of standing dead trees, carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("snagsOther_n", "branches and coarse roots of standing dead trees, nitrogen kg/ha", SqliteType.Real));
            this.Columns.Add(new("downedWood_c", "downed woody debris (yR), carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("downedWood_n", "downed woody debris (yR), nitrogen kg/ga", SqliteType.Real));
            this.Columns.Add(new("litter_c", "soil litter (yl), carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("litter_n", "soil litter (yl), nitrogen kg/ha", SqliteType.Real));
            this.Columns.Add(new("soil_c", "soil organic matter (som), carbon kg/ha", SqliteType.Real));
            this.Columns.Add(new("soil_n", "soil organic matter (som), nitrogen kg/ha", SqliteType.Real));
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            // use a condition for to control execuation for the current year
            this.yearFilter.SetExpression(projectFile.Output.Sql.Carbon.Condition);
            this.resourceUnitFilter.SetExpression(projectFile.Output.Sql.Carbon.ConditionRU);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            // global condition
            int currentCalendarYear = model.SimulationState.CurrentCalendarYear;
            if ((this.yearFilter.IsEmpty == false) && (this.yearFilter.Evaluate(currentCalendarYear) == 0.0F))
            {
                return;
            }

            bool logResourceUnitDetails = true;
            // switch off details if this is indicated in the conditionRU option
            if ((this.resourceUnitFilter.IsEmpty == false) && (this.resourceUnitFilter.Evaluate(currentCalendarYear) == 0.0F))
            {
                logResourceUnitDetails = false;
            }

            Span<float> accumulatedValues = stackalloc float[23];
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                if (resourceUnit.Snags == null)
                {
                    continue; // do not include if out of project area
                }
                Debug.Assert(resourceUnit.Soil != null, "Resource unit has null soil when its snags are non-null.");

                LiveTreeAndSaplingStatistics liveTreeAndSaplingStatistics = resourceUnit.Trees.LiveTreeAndSaplingStatisticsForAllSpecies;
                float areaFactor = resourceUnit.AreaInLandscapeInM2 / Constant.Grid.ResourceUnitAreaInM2; // conversion factor from real area to per ha values
                if (logResourceUnitDetails)
                {
                    insertRow.Parameters[0].Value = currentCalendarYear;
                    insertRow.Parameters[1].Value = resourceUnit.ID;
                    insertRow.Parameters[2].Value = areaFactor;
                    // biomass from trees (scaled to 1ha already)
                    insertRow.Parameters[3].Value = liveTreeAndSaplingStatistics.StemCarbonInKgPerHa;
                    insertRow.Parameters[4].Value = liveTreeAndSaplingStatistics.StemNitrogenInKgPerHa;
                    insertRow.Parameters[5].Value = liveTreeAndSaplingStatistics.BranchCarbonInKgPerHa;
                    insertRow.Parameters[6].Value = liveTreeAndSaplingStatistics.BranchNitrogenInKgPerHa;
                    insertRow.Parameters[7].Value = liveTreeAndSaplingStatistics.FoliageCarbonInKgPerHa;
                    insertRow.Parameters[8].Value = liveTreeAndSaplingStatistics.FoliageNitrogenInKgPerHa;
                    insertRow.Parameters[9].Value = liveTreeAndSaplingStatistics.CoarseRootCarbonInKgPerHa;
                    insertRow.Parameters[10].Value = liveTreeAndSaplingStatistics.CoarseRootNitrogenInKgPerHa;
                    insertRow.Parameters[11].Value = liveTreeAndSaplingStatistics.FineRootCarbonInKgPerHa;
                    insertRow.Parameters[12].Value = liveTreeAndSaplingStatistics.FineRootNitrogenInKgPerHa;

                    // biomass from regeneration
                    insertRow.Parameters[13].Value = liveTreeAndSaplingStatistics.RegenerationCarbonInKgPerHa;
                    insertRow.Parameters[14].Value = liveTreeAndSaplingStatistics.RegenerationNitrogenInKgPerHa;

                    // biomass from standing dead woods
                    if (resourceUnit.Snags.TotalStanding == null) // expected in year 0
                    {
                        insertRow.Parameters[15].Value = 0.0;
                        insertRow.Parameters[16].Value = 0.0;
                    }
                    else
                    {
                        insertRow.Parameters[15].Value = resourceUnit.Snags.TotalStanding.C / areaFactor;
                        insertRow.Parameters[16].Value = resourceUnit.Snags.TotalStanding.N / areaFactor;   // snags
                    }
                    if (resourceUnit.Snags.TotalBranchesAndRoots == null)
                    {
                        insertRow.Parameters[17].Value = 0.0;
                        insertRow.Parameters[18].Value = 0.0;
                    }
                    else
                    {
                        insertRow.Parameters[17].Value = resourceUnit.Snags.TotalBranchesAndRoots.C / areaFactor;
                        insertRow.Parameters[18].Value = resourceUnit.Snags.TotalBranchesAndRoots.N / areaFactor;   // snags, other (branch + coarse root)
                    }

                    // biomass from soil (convert from t/ha . kg/ha)
                    insertRow.Parameters[19].Value = resourceUnit.Soil!.YoungRefractory.C * 1000.0; // wood, 16.8.0 nullable analysis misses RU consistency check above
                    insertRow.Parameters[20].Value = resourceUnit.Soil.YoungRefractory.N * 1000.0;
                    insertRow.Parameters[21].Value = resourceUnit.Soil.YoungLabile.C * 1000.0; // litter
                    insertRow.Parameters[22].Value = resourceUnit.Soil.YoungLabile.N * 1000.0;
                    insertRow.Parameters[23].Value = resourceUnit.Soil.OrganicMatter.C * 1000.0; // soil
                    insertRow.Parameters[24].Value = resourceUnit.Soil.OrganicMatter.N * 1000.0;

                    insertRow.ExecuteNonQuery();
                }

                // landscape level statistics
                accumulatedValues[0] += areaFactor;
                // carbon pools aboveground are in kg/resource unit, e.g., the sum of stem-carbon of all trees, so no scaling required
                accumulatedValues[1] += liveTreeAndSaplingStatistics.StemCarbonInKgPerHa * areaFactor;
                accumulatedValues[2] += liveTreeAndSaplingStatistics.StemNitrogenInKgPerHa * areaFactor;
                accumulatedValues[3] += liveTreeAndSaplingStatistics.BranchCarbonInKgPerHa * areaFactor;
                accumulatedValues[4] += liveTreeAndSaplingStatistics.BranchNitrogenInKgPerHa * areaFactor;
                accumulatedValues[5] += liveTreeAndSaplingStatistics.FoliageCarbonInKgPerHa * areaFactor;
                accumulatedValues[6] += liveTreeAndSaplingStatistics.FoliageNitrogenInKgPerHa * areaFactor;
                accumulatedValues[7] += liveTreeAndSaplingStatistics.CoarseRootCarbonInKgPerHa * areaFactor;
                accumulatedValues[8] += liveTreeAndSaplingStatistics.CoarseRootNitrogenInKgPerHa * areaFactor;
                accumulatedValues[9] += liveTreeAndSaplingStatistics.FineRootCarbonInKgPerHa * areaFactor;
                accumulatedValues[10] += liveTreeAndSaplingStatistics.FineRootNitrogenInKgPerHa * areaFactor;
                // regen
                accumulatedValues[11] += liveTreeAndSaplingStatistics.RegenerationCarbonInKgPerHa;
                accumulatedValues[12] += liveTreeAndSaplingStatistics.RegenerationNitrogenInKgPerHa;
                // standing dead wood
                if (resourceUnit.Snags.TotalStanding != null)
                {
                    accumulatedValues[13] += resourceUnit.Snags.TotalStanding.C;
                    accumulatedValues[14] += resourceUnit.Snags.TotalStanding.N;
                }
                if (resourceUnit.Snags.TotalBranchesAndRoots != null)
                {
                    accumulatedValues[15] += resourceUnit.Snags.TotalBranchesAndRoots.C;
                    accumulatedValues[16] += resourceUnit.Snags.TotalBranchesAndRoots.N;
                }
                // biomass from soil (converstion to kg/ha), and scale with fraction of stockable area
                accumulatedValues[17] += 1000.0F * resourceUnit.Soil!.YoungRefractory.C * areaFactor; // 16.8.0 nullable analysis misses RU consistency check above
                accumulatedValues[18] += 1000.0F * resourceUnit.Soil.YoungRefractory.N * areaFactor;
                accumulatedValues[19] += 1000.0F * resourceUnit.Soil.YoungLabile.C * areaFactor;
                accumulatedValues[20] += 1000.0F * resourceUnit.Soil.YoungLabile.N * areaFactor;
                accumulatedValues[21] += 1000.0F * resourceUnit.Soil.OrganicMatter.C * areaFactor;
                accumulatedValues[22] += 1000.0F * resourceUnit.Soil.OrganicMatter.N * areaFactor;
            }

            // write landscape sums
            float totalStockableArea = accumulatedValues[0]; // convert to ha of stockable area
            insertRow.Parameters[0].Value = currentCalendarYear;
            insertRow.Parameters[1].Value = -1;
            insertRow.Parameters[2].Value = -1; // keys
            insertRow.Parameters[3].Value = accumulatedValues[0]; // stockable area [m2]
            for (int valueIndex = 1; valueIndex < accumulatedValues.Length; ++valueIndex)
            {
                insertRow.Parameters[2 + valueIndex].Value = accumulatedValues[valueIndex] / totalStockableArea;
            }
            insertRow.ExecuteNonQuery();
        }
    }
}
