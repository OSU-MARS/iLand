// C++/output/{ carbonout.h, carbonout.cpp }
using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Data.SqlTypes;
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
                               "The area column contains the stockable area (per resource unit / landscape) and can be used to scale to values to the actual value on the ground." + Environment.NewLine +
                               "You can use the 'condition' to control if the output should be created for the current year(see also dynamic stand output)." + Environment.NewLine +
                               "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                               "(leaving 'conditionRU' blank enables details per default).";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(new("area_ha", "Total stockable area of the resource unit, ha.", SqliteType.Real));
            this.Columns.Add(new("stem_reserve_c", "Stem and NPP reserve, kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("stem_reserve_n", "Stem and NPP reserve, kg nitrogen/ha.", SqliteType.Real));
            this.Columns.Add(new("branch_c", "Branches carbon, kg/ha.", SqliteType.Real));
            this.Columns.Add(new("branch_n", "Branches nitrogen, kg/ha.", SqliteType.Real));
            this.Columns.Add(new("foliage_c", "Foliage carbon, kg/ha.", SqliteType.Real));
            this.Columns.Add(new("foliage_n", "Foliage nitrogen, kg/ha.", SqliteType.Real));
            this.Columns.Add(new("coarseRoot_c", "Coarse root, kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("coarseRoot_n", "Coarse root, kg nitrogen/ha.", SqliteType.Real));
            this.Columns.Add(new("fineRoot_c", "Fine root, kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("fineRoot_n", "Fine root, kg nitrogen/ha.", SqliteType.Real));
            this.Columns.Add(new("regeneration_c", "Total carbon in regeneration layer (h<4m), kg/ha.", SqliteType.Real));
            this.Columns.Add(new("regeneration_n", "Total nitrogen in regeneration layer (h<4m), kg/ha.", SqliteType.Real));
            this.Columns.Add(new("snags_c", "Standing dead wood, kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("snags_n", "Standing dead wood, kg nitrogen/ha.", SqliteType.Real));
            this.Columns.Add(new("snagsOther_c", "Branches and coarse roots of standing dead trees, kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("snagsOther_n", "Branches and coarse roots of standing dead trees, kg nitrogen/ha.", SqliteType.Real));
            this.Columns.Add(new("snagsOther_branch_c", "Branches of standing dead trees (also included in snagsOther_c), kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("downedWood_c", "Downed woody debris (yR, branches, stems, coarse roots), kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("downedWood_n", "Downed woody debris (yR) kg nitrogen/ha.", SqliteType.Real));
            this.Columns.Add(new("downedWood_c_ag", "Downed woody debris aboveground (yR, stems, branches, also included in downedWood_c), kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("litter_c", "Soil litter (yl), kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("litter_n", "Soil litter (yl), kg nitrogen/ha.", SqliteType.Real));
            this.Columns.Add(new("litter_c_ag", "Soil litter aboveground (yl, foliage, part of litter_c), kg carbon/ha.", SqliteType.Real));
            this.Columns.Add(new("soil_c", "Soil organic matter (SOM), kg carbon/ha", SqliteType.Real));
            this.Columns.Add(new("soil_n", "Soil organic matter (SOM), kg nitrogen/ha", SqliteType.Real));
            this.Columns.Add(new("understory_c", "Living understory vegetation (e.g. moss), kg carbon/ha", SqliteType.Real));
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

            Span<float> accumulatedValues = stackalloc float[27];
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
                    insertRow.Parameters[0].Value = currentCalendarYear; // year
                    insertRow.Parameters[1].Value = resourceUnit.ID; // species
                    insertRow.Parameters[2].Value = areaFactor; // area_ha
                    // biomass from trees (scaled to 1ha already)
                    insertRow.Parameters[3].Value = liveTreeAndSaplingStatistics.StemAndReserveCarbonInKgPerHa; // stem_reserve_c
                    insertRow.Parameters[4].Value = liveTreeAndSaplingStatistics.StemAndReserveNitrogenInKgPerHa; // stem_reserve_n
                    insertRow.Parameters[5].Value = liveTreeAndSaplingStatistics.BranchCarbonInKgPerHa; // branch_c
                    insertRow.Parameters[6].Value = liveTreeAndSaplingStatistics.BranchNitrogenInKgPerHa; // branch_n
                    insertRow.Parameters[7].Value = liveTreeAndSaplingStatistics.FoliageCarbonInKgPerHa; // foliage_c
                    insertRow.Parameters[8].Value = liveTreeAndSaplingStatistics.FoliageNitrogenInKgPerHa; // foliage_n
                    insertRow.Parameters[9].Value = liveTreeAndSaplingStatistics.CoarseRootCarbonInKgPerHa; // coarseRoot_c
                    insertRow.Parameters[10].Value = liveTreeAndSaplingStatistics.CoarseRootNitrogenInKgPerHa; // coarseRoot_n
                    insertRow.Parameters[11].Value = liveTreeAndSaplingStatistics.FineRootCarbonInKgPerHa; // fineRoot_c
                    insertRow.Parameters[12].Value = liveTreeAndSaplingStatistics.FineRootNitrogenInKgPerHa; // fineRoot_n

                    // biomass from regeneration
                    insertRow.Parameters[13].Value = liveTreeAndSaplingStatistics.RegenerationCarbonInKgPerHa; // regeneration_c
                    insertRow.Parameters[14].Value = liveTreeAndSaplingStatistics.RegenerationNitrogenInKgPerHa; // regeneration_n

                    // biomass from standing dead woods
                    if (resourceUnit.Snags.TotalStanding == null) // expected in year 0
                    {
                        insertRow.Parameters[15].Value = Constant.Data.SqliteNaN;
                        insertRow.Parameters[16].Value = Constant.Data.SqliteNaN;
                    }
                    else
                    {
                        insertRow.Parameters[15].Value = resourceUnit.Snags.TotalStanding.C / areaFactor; // snags_c
                        insertRow.Parameters[16].Value = resourceUnit.Snags.TotalStanding.N / areaFactor; // snags_n
                    }
                    if (resourceUnit.Snags.TotalBranchesAndRoots == null)
                    {
                        insertRow.Parameters[17].Value = Constant.Data.SqliteNaN;
                        insertRow.Parameters[18].Value = Constant.Data.SqliteNaN;
                        insertRow.Parameters[19].Value = Constant.Data.SqliteNaN;
                    }
                    else
                    {
                        insertRow.Parameters[17].Value = resourceUnit.Snags.TotalBranchesAndRoots.C / areaFactor; // snagsOther_c (branch + coarse root)
                        insertRow.Parameters[18].Value = resourceUnit.Snags.TotalBranchesAndRoots.N / areaFactor; // snagsOther_n
                        insertRow.Parameters[19].Value = resourceUnit.Snags.OtherWoodAbovegroundFraction * resourceUnit.Snags.TotalBranchesAndRoots.C / areaFactor; // snagsOther_branch_c
                    }

                    // biomass from soil (convert from t/ha . kg/ha)
                    insertRow.Parameters[20].Value = 1000.0F * resourceUnit.Soil.YoungRefractory.C; // downedWood_c, 16.8.0 nullable analysis misses RU consistency check above
                    insertRow.Parameters[21].Value = 1000.0F * resourceUnit.Soil.YoungRefractory.N; // downedWood_n
                    insertRow.Parameters[22].Value = 1000.0F * resourceUnit.Soil.YoungRefractoryAbovegroundFraction * resourceUnit.Soil!.YoungRefractory.C; // downedWood_c_ag
                    insertRow.Parameters[23].Value = 1000.0F * resourceUnit.Soil.YoungLabile.C; // litter_c
                    insertRow.Parameters[24].Value = 1000.0F * resourceUnit.Soil.YoungLabile.N; // litter_n
                    insertRow.Parameters[25].Value = 1000.0F * resourceUnit.Soil.YoungLabileAbovegroundFraction * resourceUnit.Soil.YoungLabile.C; // litter_c_ag
                    insertRow.Parameters[26].Value = 1000.0F * resourceUnit.Soil.OrganicMatter.C; // soil_c
                    insertRow.Parameters[27].Value = 1000.0F * resourceUnit.Soil.OrganicMatter.N; // soil_n

                    if (resourceUnit.WaterCycle.Permafrost == null)
                    {
                        insertRow.Parameters[28].Value = Constant.Data.SqliteNaN;
                    }
                    else
                    {
                        insertRow.Parameters[28].Value = Constant.DryBiomassCarbonFraction * Constant.Grid.ResourceUnitAreaInM2 * resourceUnit.WaterCycle.Permafrost.MossBiomass; // understory_c, convert from kg/m2 -> kg C / ha
                    }

                    insertRow.ExecuteNonQuery();
                }

                // landscape level statistics
                accumulatedValues[0] += areaFactor;
                // carbon pools aboveground are in kg/resource unit, e.g., the sum of stem-carbon of all trees, so no scaling required
                accumulatedValues[1] += liveTreeAndSaplingStatistics.StemAndReserveCarbonInKgPerHa * areaFactor;
                accumulatedValues[2] += liveTreeAndSaplingStatistics.StemAndReserveNitrogenInKgPerHa * areaFactor;
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
                    accumulatedValues[15] += resourceUnit.Snags.TotalBranchesAndRoots.C; // snagsOther_c
                    accumulatedValues[16] += resourceUnit.Snags.TotalBranchesAndRoots.N; // snagsOther_n
                    accumulatedValues[17] += resourceUnit.Snags.OtherWoodAbovegroundFraction * resourceUnit.Snags.TotalBranchesAndRoots.C; // snagsOther_c_ag
                }
                // biomass from soil (converstion to kg/ha), and scale with fraction of stockable area
                accumulatedValues[18] += 1000.0F * resourceUnit.Soil!.YoungRefractory.C * areaFactor; // 16.8.0 nullable analysis misses RU consistency check above
                accumulatedValues[19] += 1000.0F * resourceUnit.Soil.YoungRefractory.N * areaFactor;
                accumulatedValues[20] += 1000.0F * resourceUnit.Soil.YoungRefractoryAbovegroundFraction * resourceUnit.Soil.YoungRefractory.C * areaFactor;
                accumulatedValues[21] += 1000.0F * resourceUnit.Soil.YoungLabile.C * areaFactor;
                accumulatedValues[22] += 1000.0F * resourceUnit.Soil.YoungLabile.N * areaFactor;
                accumulatedValues[23] += 1000.0F * resourceUnit.Soil.YoungLabileAbovegroundFraction * resourceUnit.Soil.YoungLabile.C * areaFactor;
                accumulatedValues[24] += 1000.0F * resourceUnit.Soil.OrganicMatter.C * areaFactor;
                accumulatedValues[25] += 1000.0F * resourceUnit.Soil.OrganicMatter.N * areaFactor;
                if (resourceUnit.WaterCycle.Permafrost != null)
                {
                    accumulatedValues[26] += Constant.DryBiomassCarbonFraction * Constant.Grid.ResourceUnitAreaInM2 * resourceUnit.WaterCycle.Permafrost.MossBiomass * areaFactor; // understory_c
                }
            }

            // write landscape sums
            float totalStockableArea = accumulatedValues[0]; // convert to ha of stockable area
            insertRow.Parameters[0].Value = currentCalendarYear;
            insertRow.Parameters[1].Value = -1;
            insertRow.Parameters[2].Value = -1; // keys
            insertRow.Parameters[3].Value = totalStockableArea; // stockable area [m2]
            for (int valueIndex = 1; valueIndex < accumulatedValues.Length; ++valueIndex)
            {
                insertRow.Parameters[2 + valueIndex].Value = accumulatedValues[valueIndex] / totalStockableArea;
            }
            insertRow.ExecuteNonQuery();
        }
    }
}
