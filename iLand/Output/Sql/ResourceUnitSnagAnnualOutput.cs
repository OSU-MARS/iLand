using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output.Sql
{
    public class ResourceUnitSnagAnnualOutput : AnnualOutput
    {
        public ResourceUnitSnagAnnualOutput()
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
            this.Columns.Add(new("count_ha", "tree count (that died this year)", SqliteType.Integer));
            this.Columns.Add(new("dbh_avg_cm", "average dbh (cm)", SqliteType.Real));
            this.Columns.Add(new("height_avg_m", "average tree height (m)", SqliteType.Real));
            this.Columns.Add(new("volume_m3", "volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new("basal_area_m2", "total basal area at breast height (m2)", SqliteType.Real));
            this.Columns.Add(new("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", SqliteType.Real));
            this.Columns.Add(new("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", SqliteType.Real));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                foreach (ResourceUnitTreeSpecies ruSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeStatistics ruSnagStatisticsForSpecies = ruSpecies.StatisticsSnag;
                    if (ruSnagStatisticsForSpecies.TreeCount == 0.0F)
                    {
                        continue;
                    }
                    insertRow.Parameters[0].Value = model.SimulationState.CurrentYear;
                    insertRow.Parameters[1].Value = resourceUnit.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = resourceUnit.ID;
                    insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                    insertRow.Parameters[4].Value = ruSnagStatisticsForSpecies.TreeCount;
                    insertRow.Parameters[5].Value = ruSnagStatisticsForSpecies.AverageDbh;
                    insertRow.Parameters[6].Value = ruSnagStatisticsForSpecies.AverageHeight;
                    insertRow.Parameters[7].Value = ruSnagStatisticsForSpecies.StemVolume;
                    insertRow.Parameters[8].Value = ruSnagStatisticsForSpecies.BasalArea;
                    insertRow.Parameters[9].Value = ruSnagStatisticsForSpecies.TreeNpp;
                    insertRow.Parameters[10].Value = ruSnagStatisticsForSpecies.TreeNppAboveground;
                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
