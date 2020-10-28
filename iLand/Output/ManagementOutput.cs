using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class ManagementOutput : Output
    {
        public ManagementOutput()
        {
            this.Name = "Removed trees by species/RU";
            this.TableName = "management";
            this.Description = "Aggregates for trees that are removed in current year on the level of RU x species. All values are scaled to one hectare." +
                               "The output is created after the growth of the year, " +
                               "i.e. the growth of the year in which trees are dying, is included!";

            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new SqlColumn("count_ha", "tree count (living)", OutputDatatype.Integer));
            this.Columns.Add(new SqlColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("height_avg_m", "average tree height (m)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.Double));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit ru in model.ResourceUnits)
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }

                foreach (ResourceUnitSpecies ruSpecies in ru.TreeSpecies)
                {
                    ResourceUnitSpeciesStatistics stat = ruSpecies.StatisticsManagement;
                    if (stat.TreesPerHectare == 0)
                    {
                        continue;
                    }

                    insertRow.Parameters[0].Value = model.ModelSettings.CurrentYear;
                    insertRow.Parameters[1].Value = ru.GridIndex;
                    insertRow.Parameters[2].Value = ru.EnvironmentID;
                    insertRow.Parameters[3].Value = ruSpecies.Species.ID; // keys
                    insertRow.Parameters[4].Value = stat.TreesPerHectare;
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
