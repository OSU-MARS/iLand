using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class ManagementAnnualOutput : AnnualOutput
    {
        public ManagementAnnualOutput()
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
            this.Columns.Add(new SqlColumn("count_ha", "tree count (living)", SqliteType.Integer));
            this.Columns.Add(new SqlColumn("dbh_avg_cm", "average dbh (cm)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("height_avg_m", "average tree height (m)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("volume_m3", "volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", SqliteType.Real));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }

                foreach (ResourceUnitTreeSpecies ruSpecies in ru.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeStatistics stat = ruSpecies.StatisticsManagement;
                    if (stat.TreeCount == 0)
                    {
                        continue;
                    }

                    insertRow.Parameters[0].Value = model.CurrentYear;
                    insertRow.Parameters[1].Value = ru.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = ru.ID;
                    insertRow.Parameters[3].Value = ruSpecies.Species.ID; // keys
                    insertRow.Parameters[4].Value = stat.TreeCount;
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
