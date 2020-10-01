using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace iLand.Output
{
    internal class CarbonOutput : Output
    {
        private readonly Expression mFilter; // condition for landscape-level output
        private readonly Expression mResourceUnitFilter; // condition for resource-unit-level output

        public CarbonOutput()
        {
            this.mFilter = new Expression();
            this.mResourceUnitFilter = new Expression();

            Name = "Carbon and nitrogen pools above and belowground per RU/yr";
            TableName = "carbon";
            Description = "Carbon and nitrogen pools (C and N) per resource unit / year and/or by landsacpe/year. " +
                          "On resource unit level, the outputs contain aggregated above ground pools (kg/ha) " +
                          "and below ground pools (kg/ha). " + System.Environment.NewLine +
                          "For landscape level outputs, all variables are scaled to kg/ha stockable area. " +
                          "The area column contains the stockable area (per resource unit / landscape) and can be used to scale to values to the actual value. " + System.Environment.NewLine +
                          "You can use the 'condition' to control if the output should be created for the current year(see also dynamic stand output)." + System.Environment.NewLine +
                          "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                          "(leaving 'conditionRU' blank enables details per default).";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(new SqlColumn("area_ha", "total stockable area of the resource unit (ha)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("stem_c", "Stem carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("stem_n", "Stem nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("branch_c", "branches carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("branch_n", "branches nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("foliage_c", "Foliage carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("foliage_n", "Foliage nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("coarseRoot_c", "coarse root carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("coarseRoot_n", "coarse root nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("fineRoot_c", "fine root carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("fineRoot_n", "fine root nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("regeneration_c", "total carbon in regeneration layer (h<4m) kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("regeneration_n", "total nitrogen in regeneration layer (h<4m) kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("snags_c", "standing dead wood carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("snags_n", "standing dead wood nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("snagsOther_c", "branches and coarse roots of standing dead trees, carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("snagsOther_n", "branches and coarse roots of standing dead trees, nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("downedWood_c", "downed woody debris (yR), carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("downedWood_n", "downed woody debris (yR), nitrogen kg/ga", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("litter_c", "soil litter (yl), carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("litter_n", "soil litter (yl), nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("soil_c", "soil organic matter (som), carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("soil_n", "soil organic matter (som), nitrogen kg/ha", OutputDatatype.OutDouble));
        }

        public override void Setup()
        {
            // use a condition for to control execuation for the current year
            string condition = Settings().GetString(".condition", "");
            mFilter.SetExpression(condition);

            condition = Settings().GetString(".conditionRU", "");
            mResourceUnitFilter.SetExpression(condition);
        }

        protected override void LogYear(SqliteCommand insertRow)
        {
            Model m = GlobalSettings.Instance.Model;

            // global condition
            if (!mFilter.IsEmpty && mFilter.Calculate(GlobalSettings.Instance.CurrentYear) == 0.0)
            {
                return;
            }

            bool isRUlevel = true;
            // switch off details if this is indicated in the conditionRU option
            if (!mResourceUnitFilter.IsEmpty && mResourceUnitFilter.Calculate(GlobalSettings.Instance.CurrentYear) == 0.0)
            {
                isRUlevel = false;
            }


            double[] accumulatedValues   = new double[23]; // 8 data values
            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                if (ru.ID == -1 || ru.Snags == null)
                {
                    continue; // do not include if out of project area
                }

                StandStatistics s = ru.Statistics;
                double areaFactor = ru.StockableArea / Constant.RUArea; // conversion factor from real area to per ha values
                if (isRUlevel)
                {
                    this.Add(CurrentYear());
                    this.Add(ru.Index);
                    this.Add(ru.ID);
                    this.Add(areaFactor); // keys
                    // biomass from trees (scaled to 1ha already)
                    this.Add(s.StemC);
                    this.Add(s.StemN);   // stem
                    this.Add(s.BranchC);
                    this.Add(s.BranchN);   // branch
                    this.Add(s.FoliageC);
                    this.Add(s.FoliageN);   // foliage
                    this.Add(s.CoarseRootC);
                    this.Add(s.CoarseRootN);   // coarse roots
                    this.Add(s.FineRootC);
                    this.Add(s.FineRootN);   // fine roots

                    // biomass from regeneration
                    this.Add(s.RegenerationC);
                    this.Add(s.RegenerationN);

                    // biomass from standing dead woods
                    if (ru.Snags.TotalSwd == null) // expected in year 0
                    {
                        this.Add(0.0, 0.0);
                    }
                    else
                    {
                        this.Add(ru.Snags.TotalSwd.C / areaFactor);
                        this.Add(ru.Snags.TotalSwd.N / areaFactor);   // snags
                    }
                    if (ru.Snags.TotalOtherWood == null)
                    {
                        this.Add(0.0, 0.0);
                    }
                    else
                    {
                        this.Add(ru.Snags.TotalOtherWood.C / areaFactor);
                        this.Add(ru.Snags.TotalOtherWood.N / areaFactor);   // snags, other (branch + coarse root)
                    }

                    // biomass from soil (convert from t/ha . kg/ha)
                    this.Add(ru.Soil.YoungRefractory.C * 1000.0);
                    this.Add(ru.Soil.YoungRefractory.N * 1000.0);   // wood
                    this.Add(ru.Soil.YoungLabile.C * 1000.0);
                    this.Add(ru.Soil.YoungLabile.N * 1000.0);   // litter
                    this.Add(ru.Soil.OrganicMatter.C * 1000.0);
                    this.Add(ru.Soil.OrganicMatter.N * 1000.0);   // soil

                    this.WriteRow(insertRow);
                }

                // landscape level statistics
                accumulatedValues[0] += areaFactor;
                // carbon pools aboveground are in kg/resource unit, e.g., the sum of stem-carbon of all trees, so no scaling required
                accumulatedValues[1] += s.StemC * areaFactor;
                accumulatedValues[2] += s.StemN * areaFactor;
                accumulatedValues[3] += s.BranchC * areaFactor;
                accumulatedValues[4] += s.BranchN * areaFactor;
                accumulatedValues[5] += s.FoliageC * areaFactor;
                accumulatedValues[6] += s.FoliageN * areaFactor;
                accumulatedValues[7] += s.CoarseRootC * areaFactor;
                accumulatedValues[8] += s.CoarseRootN * areaFactor;
                accumulatedValues[9] += s.FineRootC * areaFactor;
                accumulatedValues[10] += s.FineRootN * areaFactor;
                // regen
                accumulatedValues[11] += s.RegenerationC;
                accumulatedValues[12] += s.RegenerationN;
                // standing dead wood
                if (ru.Snags.TotalSwd != null)
                {
                    accumulatedValues[13] += ru.Snags.TotalSwd.C;
                    accumulatedValues[14] += ru.Snags.TotalSwd.N;
                }
                if (ru.Snags.TotalOtherWood != null)
                {
                    accumulatedValues[15] += ru.Snags.TotalOtherWood.C;
                    accumulatedValues[16] += ru.Snags.TotalOtherWood.N;
                }
                // biomass from soil (converstion to kg/ha), and scale with fraction of stockable area
                accumulatedValues[17] += ru.Soil.YoungRefractory.C * areaFactor * 1000.0;
                accumulatedValues[18] += ru.Soil.YoungRefractory.N * areaFactor * 1000.0;
                accumulatedValues[19] += ru.Soil.YoungLabile.C * areaFactor * 1000.0;
                accumulatedValues[20] += ru.Soil.YoungLabile.N * areaFactor * 1000.0;
                accumulatedValues[21] += ru.Soil.OrganicMatter.C * areaFactor * 1000.0;
                accumulatedValues[22] += ru.Soil.OrganicMatter.N * areaFactor * 1000.0;
            }

            // write landscape sums
            double totalStockableArea = accumulatedValues[0]; // convert to ha of stockable area
            this.Add(CurrentYear(), -1, -1); // keys
            this.Add(accumulatedValues[0]); // stockable area [m2]
            for (int valueIndex = 1; valueIndex < accumulatedValues.Length; ++valueIndex)
            {
                this.Add(accumulatedValues[valueIndex] / totalStockableArea);
            }
            this.WriteRow(insertRow);
        }
    }
}
