using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class StandDeadAnnualOutput : AnnualOutput
    {
        public StandDeadAnnualOutput()
        {
            this.Name = "Dead trees by species/RU";
            this.TableName = "standDead";
            this.Description = "Died trees in current year on the level of RU x species. The output is created after the growth of the year, " +
                               "i.e. the growth of year trees are dying in is included! NPP and NPP_kg are not recorded for trees that " +
                               "are removed during management.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new SqlColumn("count_ha", "tree count (that died this year)", SqliteType.Integer));
            this.Columns.Add(new SqlColumn("dbh_avg_cm", "average dbh (cm)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("height_avg_m", "average tree height (m)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("volume_m3", "volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", SqliteType.Real));
            this.Columns.Add(new SqlColumn("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", SqliteType.Real));
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
                    ResourceUnitTreeStatistics stat = ruSpecies.StatisticsDead;
                    if (stat.TreeCount == 0.0)
                    {
                        continue;
                    }
                    insertRow.Parameters[0].Value = model.CurrentYear;
                    insertRow.Parameters[1].Value = ru.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = ru.ID;
                    insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                    insertRow.Parameters[4].Value = stat.TreeCount;
                    insertRow.Parameters[5].Value = stat.AverageDbh;
                    insertRow.Parameters[6].Value = stat.AverageHeight;
                    insertRow.Parameters[7].Value = stat.StemVolume;
                    insertRow.Parameters[8].Value = stat.BasalArea;
                    insertRow.Parameters[9].Value = stat.TreeNpp;
                    insertRow.Parameters[10].Value = stat.TreeNppAboveground;
                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
