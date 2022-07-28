using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    /** StandOut is basic stand level info per species and ressource unit */
    public class ResourceUnitLiveTreeAnnualOutput : AnnualOutput
    {
        private readonly Expression yearFilter;

        public ResourceUnitLiveTreeAnnualOutput()
        {
            this.yearFilter = new();

            this.Name = "Stand by species/RU";
            this.TableName = "stand";
            this.Description = "Output of aggregates on the level of RU x species. Values are always aggregated per hectare (of stockable area). " +
                               "Use the 'area' column to scale to the actual values on the resource unit." + System.Environment.NewLine +
                               "The output is created after the growth of the year, " +
                               "i.e. output with year=2000 means effectively the state of at the end of the " +
                               "year 2000.0 The initial state (without any growth) is indicated by the year 'startyear-1'. " +
                               "You can use the 'condition' to control if the output should be created for the current year(see dynamic stand output)";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new("area_ha", "stockable forest area on the resource unit (in ha).", SqliteType.Real));
            //columns().Add(new("x_m", "x-coord", OutInteger)
            //columns().Add(new("y_m", "y-coord", OutInteger) // temp
            this.Columns.Add(new("count_ha", "tree count (living, >4m height) per ha", SqliteType.Integer));
            this.Columns.Add(new("dbh_avg_cm", "average dbh (cm)", SqliteType.Real));
            this.Columns.Add(new("height_avg_m", "average tree height (m)", SqliteType.Real));
            this.Columns.Add(new("volume_m3", "volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new("total_carbon_kg", "total carbon in living biomass (aboveground compartments and roots) of all living trees (including regeneration layer) (kg/ha)", SqliteType.Real));
            this.Columns.Add(new("gwl_m3", "'gesamtwuchsleistung' (total growth including removed/dead trees) volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new("basal_area_m2", "total basal area at breast height (m2)", SqliteType.Real));
            this.Columns.Add(new("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", SqliteType.Real));
            this.Columns.Add(new("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", SqliteType.Real));
            this.Columns.Add(new("LAI", "Leafareaindex (m2/m2)", SqliteType.Real));
            this.Columns.Add(new("cohort_count_ha", "number of cohorts in the regeneration layer (<4m) /ha", SqliteType.Integer));
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            this.yearFilter.SetExpression(projectFile.Output.Sql.Stand.Condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (this.yearFilter.IsEmpty == false)
            {
                if (this.yearFilter.Evaluate(model.SimulationState.CurrentYear) == 0.0)
                {
                    return;
                }
            }

            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                foreach (ResourceUnitTreeSpecies ruSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeStatistics ruTreeStatisticsForSpecies = ruSpecies.StatisticsLive;
                    if (ruTreeStatisticsForSpecies.TreeCount == 0 && ruTreeStatisticsForSpecies.CohortCount == 0)
                    {
                        continue;
                    }
                    insertRow.Parameters[0].Value = model.SimulationState.CurrentYear;
                    insertRow.Parameters[1].Value = resourceUnit.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = resourceUnit.ID;
                    insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                    insertRow.Parameters[4].Value = resourceUnit.AreaInLandscapeInM2 / Constant.ResourceUnitAreaInM2; // keys
                    // insertRow.Parameters[4].Value = ru.boundingBox().center().x() << ru.boundingBox().center().y();  // temp
                    insertRow.Parameters[5].Value = ruTreeStatisticsForSpecies.TreeCount;
                    insertRow.Parameters[6].Value = ruTreeStatisticsForSpecies.AverageDbh;
                    insertRow.Parameters[7].Value = ruTreeStatisticsForSpecies.AverageHeight;
                    insertRow.Parameters[8].Value = ruTreeStatisticsForSpecies.StemVolume;
                    insertRow.Parameters[9].Value = ruTreeStatisticsForSpecies.GetTotalCarbon();
                    insertRow.Parameters[10].Value = ruTreeStatisticsForSpecies.LiveAndSnagStemVolume;
                    insertRow.Parameters[11].Value = ruTreeStatisticsForSpecies.BasalArea;
                    insertRow.Parameters[12].Value = ruTreeStatisticsForSpecies.TreeNpp;
                    insertRow.Parameters[13].Value = ruTreeStatisticsForSpecies.TreeNppAboveground;
                    insertRow.Parameters[14].Value = ruTreeStatisticsForSpecies.LeafAreaIndex;
                    insertRow.Parameters[15].Value = ruTreeStatisticsForSpecies.CohortCount;
                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
