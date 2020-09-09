using iLand.core;
using iLand.tools;

namespace iLand.output
{
    internal class StandDeadOut : Output
    {
        public StandDeadOut()
        {
            setName("Dead trees by species/RU", "standdead");
            setDescription("Died trees in current year on the level of RU x species. The output is created after the growth of the year, " +
                       "i.e. the growth of year trees are dying in is included! NPP and NPP_kg are not recorded for trees that " +
                       "are removed during management.");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(OutputColumn.species());
            columns().Add(new OutputColumn("count_ha", "tree count (that died this year)", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("height_avg_m", "average tree height (m)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", OutputDatatype.OutDouble));

        }

        public override void exec()
        {
            Model m = GlobalSettings.instance().model();

            foreach (ResourceUnit ru in m.ruList())
            {
                if (ru.id() == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.ruSpecies())
                {
                    StandStatistics stat = rus.constStatisticsDead();
                    if (stat.count() == 0.0)
                    {
                        continue;
                    }
                    this.add(currentYear());
                    this.add(ru.index());
                    this.add(ru.id());
                    this.add(rus.species().id()); // keys
                    this.add(stat.count());
                    this.add(stat.dbh_avg());
                    this.add(stat.height_avg());
                    this.add(stat.volume());
                    this.add(stat.basalArea());
                    this.add(stat.npp());
                    this.add(stat.nppAbove());
                    writeRow();
                }
            }
        }
    }
}
