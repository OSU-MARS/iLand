using iLand.core;
using iLand.tools;

namespace iLand.output
{
    /** StandOut is basic stand level info per species and ressource unit */
    internal class StandOut : Output
    {
        private Expression mCondition;

        public StandOut()
        {
            setName("Stand by species/RU", "stand");
            setDescription("Output of aggregates on the level of RU x species. Values are always aggregated per hectare (of stockable area). " +
                       "Use the 'area' column to scale to the actual values on the resource unit." + System.Environment.NewLine +
                       "The output is created after the growth of the year, " +
                       "i.e. output with year=2000 means effectively the state of at the end of the " +
                       "year 2000.0 The initial state (without any growth) is indicated by the year 'startyear-1'. " +
                       "You can use the 'condition' to control if the output should be created for the current year(see dynamic stand output)");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(OutputColumn.species());
            columns().Add(new OutputColumn("area_ha", "stockable forest area on the resource unit (in ha).", OutputDatatype.OutDouble));
            //columns().Add(new OutputColumn("x_m", "x-coord", OutInteger)
            //columns().Add(new OutputColumn("y_m", "y-coord", OutInteger) // temp
            columns().Add(new OutputColumn("count_ha", "tree count (living, >4m height) per ha", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("height_avg_m", "average tree height (m)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("total_carbon_kg", "total carbon in living biomass (aboveground compartments and roots) of all living trees (including regeneration layer) (kg/ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("gwl_m3", "'gesamtwuchsleistung' (total growth including removed/dead trees) volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("LAI", "Leafareaindex (m2/m2)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("cohort_count_ha", "number of cohorts in the regeneration layer (<4m) /ha", OutputDatatype.OutInteger));
        }

        public void setup()
        {
            // use a condition for to control execuation for the current year
            string condition = settings().value(".condition", "");
            mCondition.setExpression(condition);
        }

        public override void exec()
        {
            Model m = GlobalSettings.instance().model();
            if (!mCondition.isEmpty())
            {
                if (mCondition.calculate(GlobalSettings.instance().currentYear()) == 0.0)
                {
                    return;
                }
            }

            foreach (ResourceUnit ru in m.ruList())
            {
                if (ru.id() == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.ruSpecies())
                {
                    StandStatistics stat = rus.constStatistics();
                    if (stat.count() == 0 && stat.cohortCount() == 0)
                    {
                        continue;
                    }
                    this.add(currentYear());
                    this.add(ru.index());
                    this.add(ru.id());
                    this.add(rus.species().id());
                    this.add(ru.stockableArea() / Constant.cRUArea); // keys
                    // this.add(ru.boundingBox().center().x() << ru.boundingBox().center().y());  // temp
                    this.add(stat.count());
                    this.add(stat.dbh_avg());
                    this.add(stat.height_avg());
                    this.add(stat.volume());
                    this.add(stat.totalCarbon());
                    this.add(stat.gwl());
                    this.add(stat.basalArea());
                    this.add(stat.npp());
                    this.add(stat.nppAbove());
                    this.add(stat.leafAreaIndex());
                    this.add(stat.cohortCount());
                    writeRow();
                }
            }
        }
    }
}
