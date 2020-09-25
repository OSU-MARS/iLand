using iLand.core;
using iLand.tools;
using System.Collections.Generic;

namespace iLand.output
{
    internal class CarbonOut : Output
    {
        private readonly Expression mCondition; // condition for landscape-level output
        private readonly Expression mConditionDetails; // condition for resource-unit-level output

        public CarbonOut()
        {
            this.mCondition = new Expression();
            this.mConditionDetails = new Expression();

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
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateResourceUnit());
            Columns.Add(OutputColumn.CreateID());
            Columns.Add(new OutputColumn("area_ha", "total stockable area of the resource unit (ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("stem_c", "Stem carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("stem_n", "Stem nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("branch_c", "branches carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("branch_n", "branches nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("foliage_c", "Foliage carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("foliage_n", "Foliage nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("coarseRoot_c", "coarse root carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("coarseRoot_n", "coarse root nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("fineRoot_c", "fine root carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("fineRoot_n", "fine root nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("regeneration_c", "total carbon in regeneration layer (h<4m) kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("regeneration_n", "total nitrogen in regeneration layer (h<4m) kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("snags_c", "standing dead wood carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("snags_n", "standing dead wood nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("snagsOther_c", "branches and coarse roots of standing dead trees, carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("snagsOther_n", "branches and coarse roots of standing dead trees, nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("downedWood_c", "downed woody debris (yR), carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("downedWood_n", "downed woody debris (yR), nitrogen kg/ga", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("litter_c", "soil litter (yl), carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("litter_n", "soil litter (yl), nitrogen kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("soil_c", "soil organic matter (som), carbon kg/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("soil_n", "soil organic matter (som), nitrogen kg/ha", OutputDatatype.OutDouble));
        }

        public override void Setup()
        {
            // use a condition for to control execuation for the current year
            string condition = Settings().Value(".condition", "");
            mCondition.SetExpression(condition);

            condition = Settings().Value(".conditionRU", "");
            mConditionDetails.SetExpression(condition);
        }

        public override void Exec()
        {
            Model m = GlobalSettings.Instance.Model;

            // global condition
            if (!mCondition.IsEmpty && mCondition.Calculate(GlobalSettings.Instance.CurrentYear) == 0.0)
            {
                return;
            }

            bool ru_level = true;
            // switch off details if this is indicated in the conditionRU option
            if (!mConditionDetails.IsEmpty && mConditionDetails.Calculate(GlobalSettings.Instance.CurrentYear) == 0.0)
            {
                ru_level = false;
            }


            List<double> v = new List<double>(23); // 8 data values
            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                if (ru.ID == -1 || ru.Snags == null)
                {
                    continue; // do not include if out of project area
                }

                StandStatistics s = ru.Statistics;
                double area_factor = ru.StockableArea / Constant.RUArea; // conversion factor from real area to per ha values
                if (ru_level)
                {
                    this.Add(CurrentYear());
                    this.Add(ru.Index);
                    this.Add(ru.ID);
                    this.Add(area_factor); // keys
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

                    // biomass from standing dead wood
                    this.Add(ru.Snags.TotalSwd.C / area_factor);
                    this.Add(ru.Snags.TotalSwd.N / area_factor);   // snags
                    this.Add(ru.Snags.TotalOtherWood.C / area_factor);
                    this.Add(ru.Snags.TotalOtherWood.N / area_factor);   // snags, other (branch + coarse root)

                    // biomass from soil (convert from t/ha . kg/ha)
                    this.Add(ru.Soil.YoungRefractory.C * 1000.0);
                    this.Add(ru.Soil.YoungRefractory.N * 1000.0);   // wood
                    this.Add(ru.Soil.YoungLabile.C * 1000.0);
                    this.Add(ru.Soil.YoungLabile.N * 1000.0);   // litter
                    this.Add(ru.Soil.OrganicMatter.C * 1000.0);
                    this.Add(ru.Soil.OrganicMatter.N * 1000.0);   // soil

                    WriteRow();
                }
                // landscape level statistics

                v.Add(area_factor);
                // carbon pools aboveground are in kg/resource unit, e.g., the sum of stem-carbon of all trees, so no scaling required
                v.Add(s.StemC * area_factor);
                v.Add(s.StemN * area_factor);
                v.Add(s.BranchC * area_factor);
                v.Add(s.BranchN * area_factor);
                v.Add(s.FoliageC * area_factor);
                v.Add(s.FoliageN * area_factor);
                v.Add(s.CoarseRootC * area_factor);
                v.Add(s.CoarseRootN * area_factor);
                v.Add(s.FineRootC * area_factor);
                v.Add(s.FineRootN * area_factor);
                // regen
                v.Add(s.RegenerationC);
                v.Add(s.RegenerationN);
                // standing dead wood
                v.Add(ru.Snags.TotalSwd.C);
                v.Add(ru.Snags.TotalSwd.N);
                v.Add(ru.Snags.TotalOtherWood.C);
                v.Add(ru.Snags.TotalOtherWood.N);
                // biomass from soil (converstion to kg/ha), and scale with fraction of stockable area
                v.Add(ru.Soil.YoungRefractory.C * area_factor * 1000.0);
                v.Add(ru.Soil.YoungRefractory.N * area_factor * 1000.0);
                v.Add(ru.Soil.YoungLabile.C * area_factor * 1000.0);
                v.Add(ru.Soil.YoungLabile.N * area_factor * 1000.0);
                v.Add(ru.Soil.OrganicMatter.C * area_factor * 1000.0);
                v.Add(ru.Soil.OrganicMatter.N * area_factor * 1000.0);
            }

            // write landscape sums
            double total_stockable_area = v[0]; // convert to ha of stockable area
            this.Add(CurrentYear());
            this.Add(-1);
            this.Add(-1); // keys
            this.Add(v[0]); // stockable area [m2]
            for (int i = 1; i < v.Count; ++i)
            {
                this.Add(v[i] / total_stockable_area);
            }
            WriteRow();
        }
    }
}
