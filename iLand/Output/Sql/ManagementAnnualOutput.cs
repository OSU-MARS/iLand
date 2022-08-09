using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output.Sql
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
            this.Columns.Add(new("count_ha", "tree count (living)", SqliteType.Integer));
            this.Columns.Add(new("dbh_avg_cm", "average dbh (cm)", SqliteType.Real));
            this.Columns.Add(new("height_avg_m", "average tree height (m)", SqliteType.Real));
            this.Columns.Add(new("volume_m3", "volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new("basal_area_m2", "total basal area at breast height (m2)", SqliteType.Real));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                foreach (ResourceUnitTreeSpecies ruSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeSpeciesStatistics ruManagementEffects = ruSpecies.StatisticsManagement;
                    if (ruManagementEffects.TreesPerHa == 0)
                    {
                        continue;
                    }

                    insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                    insertRow.Parameters[1].Value = resourceUnit.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = resourceUnit.ID;
                    insertRow.Parameters[3].Value = ruSpecies.Species.ID; // keys
                    insertRow.Parameters[4].Value = ruManagementEffects.TreesPerHa;
                    insertRow.Parameters[5].Value = ruManagementEffects.AverageDbhInCm;
                    insertRow.Parameters[6].Value = ruManagementEffects.AverageHeightInM;
                    insertRow.Parameters[7].Value = ruManagementEffects.StemVolumeInM3PerHa;
                    insertRow.Parameters[8].Value = ruManagementEffects.BasalAreaInM2PerHa;

                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
