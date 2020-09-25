using iLand.core;
using iLand.tools;

namespace iLand.output
{
    internal class ManagementOut : Output
    {
        public ManagementOut()
        {
            Name = "Removed trees by species/RU";
            TableName = "management";
            Description = "Aggregates for trees that are removed in current year on the level of RU x species. All values are scaled to one hectare." +
                          "The output is created after the growth of the year, " +
                          "i.e. the growth of the year in which trees are dying, is included!";

            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateResourceUnit());
            Columns.Add(OutputColumn.CreateID());
            Columns.Add(OutputColumn.CreateSpecies());
            Columns.Add(new OutputColumn("count_ha", "tree count (living)", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("height_avg_m", "average tree height (m)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.OutDouble));
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
                    StandStatistics stat = rus.StatisticsMgmt;
                    if (stat.Count == 0)
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

                    WriteRow();
                }
            }
        }
    }
}
