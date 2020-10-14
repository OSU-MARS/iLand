using iLand.Core;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class ManagementOutput : Output
    {
        public ManagementOutput()
        {
            Name = "Removed trees by species/RU";
            TableName = "management";
            Description = "Aggregates for trees that are removed in current year on the level of RU x species. All values are scaled to one hectare." +
                          "The output is created after the growth of the year, " +
                          "i.e. the growth of the year in which trees are dying, is included!";

            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(SqlColumn.CreateSpecies());
            Columns.Add(new SqlColumn("count_ha", "tree count (living)", OutputDatatype.Integer));
            Columns.Add(new SqlColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("height_avg_m", "average tree height (m)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.Double));
            Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.Double));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit ru in model.ResourceUnits)
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

                    insertRow.Parameters[0].Value = model.GlobalSettings.CurrentYear;
                    insertRow.Parameters[1].Value = ru.Index;
                    insertRow.Parameters[2].Value = ru.ID;
                    insertRow.Parameters[3].Value = rus.Species.ID; // keys
                    insertRow.Parameters[4].Value = stat.Count;
                    insertRow.Parameters[5].Value = stat.AverageDbh;
                    insertRow.Parameters[6].Value = stat.AverageHeight;
                    insertRow.Parameters[7].Value = stat.StemVolume;
                    insertRow.Parameters[8].Value = stat.BasalArea;

                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
