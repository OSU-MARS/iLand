using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;

namespace iLand.Output
{
    public class CarbonOutput : Output
    {
        private readonly Expression mFilter; // condition for landscape-level output
        private readonly Expression mResourceUnitFilter; // condition for resource-unit-level output

        public CarbonOutput()
        {
            this.mFilter = new Expression();
            this.mResourceUnitFilter = new Expression();

            this.Name = "Carbon and nitrogen pools above and belowground per RU/yr";
            this.TableName = "carbon";
            this.Description = "Carbon and nitrogen pools (C and N) per resource unit / year and/or by landsacpe/year. " +
                               "On resource unit level, the outputs contain aggregated above ground pools (kg/ha) " +
                               "and below ground pools (kg/ha). " + System.Environment.NewLine +
                               "For landscape level outputs, all variables are scaled to kg/ha stockable area. " +
                               "The area column contains the stockable area (per resource unit / landscape) and can be used to scale to values to the actual value. " + System.Environment.NewLine +
                               "You can use the 'condition' to control if the output should be created for the current year(see also dynamic stand output)." + System.Environment.NewLine +
                               "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                               "(leaving 'conditionRU' blank enables details per default).";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(new SqlColumn("area_ha", "total stockable area of the resource unit (ha)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("stem_c", "Stem carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("stem_n", "Stem nitrogen kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("branch_c", "branches carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("branch_n", "branches nitrogen kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("foliage_c", "Foliage carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("foliage_n", "Foliage nitrogen kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("coarseRoot_c", "coarse root carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("coarseRoot_n", "coarse root nitrogen kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("fineRoot_c", "fine root carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("fineRoot_n", "fine root nitrogen kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("regeneration_c", "total carbon in regeneration layer (h<4m) kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("regeneration_n", "total nitrogen in regeneration layer (h<4m) kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("snags_c", "standing dead wood carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("snags_n", "standing dead wood nitrogen kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("snagsOther_c", "branches and coarse roots of standing dead trees, carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("snagsOther_n", "branches and coarse roots of standing dead trees, nitrogen kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("downedWood_c", "downed woody debris (yR), carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("downedWood_n", "downed woody debris (yR), nitrogen kg/ga", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("litter_c", "soil litter (yl), carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("litter_n", "soil litter (yl), nitrogen kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("soil_c", "soil organic matter (som), carbon kg/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("soil_n", "soil organic matter (som), nitrogen kg/ha", OutputDatatype.Double));
        }

        public override void Setup(Model model)
        {
            // use a condition for to control execuation for the current year
            this.mFilter.SetExpression(model.Project.Output.Carbon.Condition);
            this.mResourceUnitFilter.SetExpression(model.Project.Output.Carbon.ConditionRU);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            // global condition
            if (!mFilter.IsEmpty && mFilter.Evaluate(model.CurrentYear) == 0.0)
            {
                return;
            }

            bool isRUlevel = true;
            // switch off details if this is indicated in the conditionRU option
            if (!mResourceUnitFilter.IsEmpty && mResourceUnitFilter.Evaluate(model.CurrentYear) == 0.0)
            {
                isRUlevel = false;
            }

            float[] accumulatedValues   = new float[23]; // 8 data values
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                if (ru.EnvironmentID == -1 || ru.Snags == null)
                {
                    continue; // do not include if out of project area
                }
                Debug.Assert(ru.Snags != null, "Resource unit has null soil when its snags are non-null.");
                
                ResourceUnitTreeStatistics ruStatistics = ru.Trees.Statistics;
                float areaFactor = ru.AreaInLandscape / Constant.RUArea; // conversion factor from real area to per ha values
                if (isRUlevel)
                {
                    insertRow.Parameters[0].Value = model.CurrentYear;
                    insertRow.Parameters[1].Value = ru.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = ru.EnvironmentID;
                    insertRow.Parameters[3].Value = areaFactor;
                    // biomass from trees (scaled to 1ha already)
                    insertRow.Parameters[4].Value = ruStatistics.StemC[^1];
                    insertRow.Parameters[5].Value = ruStatistics.StemN[^1];
                    insertRow.Parameters[6].Value = ruStatistics.BranchC[^1];
                    insertRow.Parameters[7].Value = ruStatistics.BranchN[^1];
                    insertRow.Parameters[8].Value = ruStatistics.FoliageC[^1];
                    insertRow.Parameters[9].Value = ruStatistics.FoliageN[^1];
                    insertRow.Parameters[10].Value = ruStatistics.CoarseRootC[^1];
                    insertRow.Parameters[11].Value = ruStatistics.CoarseRootN[^1];
                    insertRow.Parameters[12].Value = ruStatistics.FineRootC[^1];
                    insertRow.Parameters[13].Value = ruStatistics.FineRootN[^1];

                    // biomass from regeneration
                    insertRow.Parameters[14].Value = ruStatistics.RegenerationC[^1];
                    insertRow.Parameters[15].Value = ruStatistics.RegenerationN[^1];

                    // biomass from standing dead woods
                    if (ru.Snags.TotalStanding == null) // expected in year 0
                    {
                        insertRow.Parameters[16].Value = 0.0;
                        insertRow.Parameters[17].Value = 0.0;
                    }
                    else
                    {
                        insertRow.Parameters[16].Value = ru.Snags.TotalStanding.C / areaFactor;
                        insertRow.Parameters[17].Value = ru.Snags.TotalStanding.N / areaFactor;   // snags
                    }
                    if (ru.Snags.TotalBranchesAndRoots == null)
                    {
                        insertRow.Parameters[18].Value = 0.0;
                        insertRow.Parameters[19].Value = 0.0;
                    }
                    else
                    {
                        insertRow.Parameters[18].Value = ru.Snags.TotalBranchesAndRoots.C / areaFactor;
                        insertRow.Parameters[19].Value = ru.Snags.TotalBranchesAndRoots.N / areaFactor;   // snags, other (branch + coarse root)
                    }

                    // biomass from soil (convert from t/ha . kg/ha)
                    insertRow.Parameters[20].Value = ru.Soil!.YoungRefractory.C * 1000.0; // wood, 16.8.0 nullable analysis misses RU consistency check above
                    insertRow.Parameters[21].Value = ru.Soil.YoungRefractory.N * 1000.0;
                    insertRow.Parameters[22].Value = ru.Soil.YoungLabile.C * 1000.0; // litter
                    insertRow.Parameters[23].Value = ru.Soil.YoungLabile.N * 1000.0;
                    insertRow.Parameters[24].Value = ru.Soil.OrganicMatter.C * 1000.0; // soil
                    insertRow.Parameters[25].Value = ru.Soil.OrganicMatter.N * 1000.0;

                    insertRow.ExecuteNonQuery();
                }

                // landscape level statistics
                accumulatedValues[0] += areaFactor;
                // carbon pools aboveground are in kg/resource unit, e.g., the sum of stem-carbon of all trees, so no scaling required
                accumulatedValues[1] += ruStatistics.StemC[^1] * areaFactor;
                accumulatedValues[2] += ruStatistics.StemN[^1] * areaFactor;
                accumulatedValues[3] += ruStatistics.BranchC[^1] * areaFactor;
                accumulatedValues[4] += ruStatistics.BranchN[^1] * areaFactor;
                accumulatedValues[5] += ruStatistics.FoliageC[^1] * areaFactor;
                accumulatedValues[6] += ruStatistics.FoliageN[^1] * areaFactor;
                accumulatedValues[7] += ruStatistics.CoarseRootC[^1] * areaFactor;
                accumulatedValues[8] += ruStatistics.CoarseRootN[^1] * areaFactor;
                accumulatedValues[9] += ruStatistics.FineRootC[^1] * areaFactor;
                accumulatedValues[10] += ruStatistics.FineRootN[^1] * areaFactor;
                // regen
                accumulatedValues[11] += ruStatistics.RegenerationC[^1];
                accumulatedValues[12] += ruStatistics.RegenerationN[^1];
                // standing dead wood
                if (ru.Snags.TotalStanding != null)
                {
                    accumulatedValues[13] += ru.Snags.TotalStanding.C;
                    accumulatedValues[14] += ru.Snags.TotalStanding.N;
                }
                if (ru.Snags.TotalBranchesAndRoots != null)
                {
                    accumulatedValues[15] += ru.Snags.TotalBranchesAndRoots.C;
                    accumulatedValues[16] += ru.Snags.TotalBranchesAndRoots.N;
                }
                // biomass from soil (converstion to kg/ha), and scale with fraction of stockable area
                accumulatedValues[17] += 1000.0F * ru.Soil!.YoungRefractory.C * areaFactor; // 16.8.0 nullable analysis misses RU consistency check above
                accumulatedValues[18] += 1000.0F * ru.Soil.YoungRefractory.N * areaFactor;
                accumulatedValues[19] += 1000.0F * ru.Soil.YoungLabile.C * areaFactor;
                accumulatedValues[20] += 1000.0F * ru.Soil.YoungLabile.N * areaFactor;
                accumulatedValues[21] += 1000.0F * ru.Soil.OrganicMatter.C * areaFactor;
                accumulatedValues[22] += 1000.0F * ru.Soil.OrganicMatter.N * areaFactor;
            }

            // write landscape sums
            double totalStockableArea = accumulatedValues[0]; // convert to ha of stockable area
            insertRow.Parameters[0].Value = model.CurrentYear;
            insertRow.Parameters[1].Value = -1;
            insertRow.Parameters[2].Value = -1; // keys
            insertRow.Parameters[3].Value = accumulatedValues[0]; // stockable area [m2]
            for (int valueIndex = 1; valueIndex < accumulatedValues.Length; ++valueIndex)
            {
                insertRow.Parameters[3 + valueIndex].Value = accumulatedValues[valueIndex] / totalStockableArea;
            }
            insertRow.ExecuteNonQuery();
        }
    }
}
