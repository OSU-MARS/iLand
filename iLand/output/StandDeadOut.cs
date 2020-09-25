using iLand.core;
using iLand.tools;

namespace iLand.output
{
    internal class StandDeadOut : Output
    {
        public StandDeadOut()
        {
            Name = "Dead trees by species/RU";
            TableName = "standdead";
            Description = "Died trees in current year on the level of RU x species. The output is created after the growth of the year, " +
                          "i.e. the growth of year trees are dying in is included! NPP and NPP_kg are not recorded for trees that " +
                          "are removed during management.";
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateResourceUnit());
            Columns.Add(OutputColumn.CreateID());
            Columns.Add(OutputColumn.CreateSpecies());
            Columns.Add(new OutputColumn("count_ha", "tree count (that died this year)", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("height_avg_m", "average tree height (m)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", OutputDatatype.OutDouble));
        }

        public override void Exec()
        {
            Model m = GlobalSettings.Instance.Model;

            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.Species)
                {
                    StandStatistics stat = rus.StatisticsDead;
                    if (stat.Count == 0.0)
                    {
                        continue;
                    }
                    this.Add(CurrentYear());
                    this.Add(ru.Index);
                    this.Add(ru.ID);
                    this.Add(rus.Species.ID); // keys
                    this.Add(stat.Count);
                    this.Add(stat.AverageDbh);
                    this.Add(stat.AverageHeight);
                    this.Add(stat.Volume);
                    this.Add(stat.BasalArea);
                    this.Add(stat.Npp);
                    this.Add(stat.NppAbove);
                    WriteRow();
                }
            }
        }
    }
}
