using iLand.core;
using iLand.tools;
using System.Collections.Generic;

namespace iLand.output
{
    internal class CarbonOut : Output
    {
        private Expression mCondition; // condition for landscape-level output
        private Expression mConditionDetails; // condition for resource-unit-level output

        public CarbonOut()
        {
            setName("Carbon and nitrogen pools above and belowground per RU/yr", "carbon");
            setDescription("Carbon and nitrogen pools (C and N) per resource unit / year and/or by landsacpe/year. " +
                       "On resource unit level, the outputs contain aggregated above ground pools (kg/ha) " +
                       "and below ground pools (kg/ha). " + System.Environment.NewLine +
                       "For landscape level outputs, all variables are scaled to kg/ha stockable area. " +
                       "The area column contains the stockable area (per resource unit / landscape) and can be used to scale to values to the actual value. " + System.Environment.NewLine +
                       "You can use the 'condition' to control if the output should be created for the current year(see also dynamic stand output)." + System.Environment.NewLine +
                       "The 'conditionRU' can be used to suppress resource-unit-level details; eg. specifying 'in(year,100,200,300)' limits output on reosurce unit level to the years 100,200,300 " +
                       "(leaving 'conditionRU' blank enables details per default).");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(new OutputColumn("area_ha", "total stockable area of the resource unit (ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("stem_c", "Stem carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("stem_n", "Stem nitrogen kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("branch_c", "branches carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("branch_n", "branches nitrogen kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("foliage_c", "Foliage carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("foliage_n", "Foliage nitrogen kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("coarseRoot_c", "coarse root carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("coarseRoot_n", "coarse root nitrogen kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("fineRoot_c", "fine root carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("fineRoot_n", "fine root nitrogen kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("regeneration_c", "total carbon in regeneration layer (h<4m) kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("regeneration_n", "total nitrogen in regeneration layer (h<4m) kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("snags_c", "standing dead wood carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("snags_n", "standing dead wood nitrogen kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("snagsOther_c", "branches and coarse roots of standing dead trees, carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("snagsOther_n", "branches and coarse roots of standing dead trees, nitrogen kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("downedWood_c", "downed woody debris (yR), carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("downedWood_n", "downed woody debris (yR), nitrogen kg/ga", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("litter_c", "soil litter (yl), carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("litter_n", "soil litter (yl), nitrogen kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("soil_c", "soil organic matter (som), carbon kg/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("soil_n", "soil organic matter (som), nitrogen kg/ha", OutputDatatype.OutDouble));
        }

        public void setup()
        {
            // use a condition for to control execuation for the current year
            string condition = settings().value(".condition", "");
            mCondition.setExpression(condition);

            condition = settings().value(".conditionRU", "");
            mConditionDetails.setExpression(condition);
        }


        public override void exec()
        {
            Model m = GlobalSettings.instance().model();

            // global condition
            if (!mCondition.isEmpty() && mCondition.calculate(GlobalSettings.instance().currentYear()) == 0.0)
            {
                return;
            }

            bool ru_level = true;
            // switch off details if this is indicated in the conditionRU option
            if (!mConditionDetails.isEmpty() && mConditionDetails.calculate(GlobalSettings.instance().currentYear()) == 0.0)
            {
                ru_level = false;
            }


            List<double> v = new List<double>(23); // 8 data values
            foreach (ResourceUnit ru in m.ruList())
            {
                if (ru.id() == -1 || ru.snag() == null)
                {
                    continue; // do not include if out of project area
                }

                StandStatistics s = ru.statistics();
                int ru_count = 0;
                double area_factor = ru.stockableArea() / Constant.cRUArea; // conversion factor from real area to per ha values
                if (ru_level)
                {
                    this.add(currentYear());
                    this.add(ru.index());
                    this.add(ru.id());
                    this.add(area_factor); // keys
                    // biomass from trees (scaled to 1ha already)
                    this.add(s.cStem());
                    this.add(s.nStem());   // stem
                    this.add(s.cBranch());
                    this.add(s.nBranch());   // branch
                    this.add(s.cFoliage());
                    this.add(s.nFoliage());   // foliage
                    this.add(s.cCoarseRoot());
                    this.add(s.nCoarseRoot());   // coarse roots
                    this.add(s.cFineRoot());
                    this.add(s.nFineRoot());   // fine roots

                    // biomass from regeneration
                    this.add(s.cRegeneration());
                    this.add(s.nRegeneration());

                    // biomass from standing dead wood
                    this.add(ru.snag().totalSWD().C / area_factor);
                    this.add(ru.snag().totalSWD().N / area_factor);   // snags
                    this.add(ru.snag().totalOtherWood().C / area_factor);
                    this.add(ru.snag().totalOtherWood().N / area_factor);   // snags, other (branch + coarse root)

                    // biomass from soil (convert from t/ha . kg/ha)
                    this.add(ru.soil().youngRefractory().C * 1000.0);
                    this.add(ru.soil().youngRefractory().N * 1000.0);   // wood
                    this.add(ru.soil().youngLabile().C * 1000.0);
                    this.add(ru.soil().youngLabile().N * 1000.0);   // litter
                    this.add(ru.soil().oldOrganicMatter().C * 1000.0);
                    this.add(ru.soil().oldOrganicMatter().N * 1000.0);   // soil

                    writeRow();
                }
                // landscape level statistics

                ++ru_count;
                v.Add(area_factor);
                // carbon pools aboveground are in kg/resource unit, e.g., the sum of stem-carbon of all trees, so no scaling required
                v.Add(s.cStem() * area_factor);
                v.Add(s.nStem() * area_factor);
                v.Add(s.cBranch() * area_factor);
                v.Add(s.nBranch() * area_factor);
                v.Add(s.cFoliage() * area_factor);
                v.Add(s.nFoliage() * area_factor);
                v.Add(s.cCoarseRoot() * area_factor);
                v.Add(s.nCoarseRoot() * area_factor);
                v.Add(s.cFineRoot() * area_factor);
                v.Add(s.nFineRoot() * area_factor);
                // regen
                v.Add(s.cRegeneration());
                v.Add(s.nRegeneration());
                // standing dead wood
                v.Add(ru.snag().totalSWD().C);
                v.Add(ru.snag().totalSWD().N);
                v.Add(ru.snag().totalOtherWood().C);
                v.Add(ru.snag().totalOtherWood().N);
                // biomass from soil (converstion to kg/ha), and scale with fraction of stockable area
                v.Add(ru.soil().youngRefractory().C * area_factor * 1000.0);
                v.Add(ru.soil().youngRefractory().N * area_factor * 1000.0);
                v.Add(ru.soil().youngLabile().C * area_factor * 1000.0);
                v.Add(ru.soil().youngLabile().N * area_factor * 1000.0);
                v.Add(ru.soil().oldOrganicMatter().C * area_factor * 1000.0);
                v.Add(ru.soil().oldOrganicMatter().N * area_factor * 1000.0);
            }
            // write landscape sums
            double total_stockable_area = v[0]; // convert to ha of stockable area
            this.add(currentYear());
            this.add(-1);
            this.add(-1); // keys
            this.add(v[0]); // stockable area [m2]
            for (int i = 1; i < v.Count; ++i)
            {
                this.add(v[i] / total_stockable_area);
            }
            writeRow();

        }

    }
}
