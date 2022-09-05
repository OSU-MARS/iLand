using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    // resource unit level statistics per tree species
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
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(SqlColumn.CreateTreeSpeciesID());
            this.Columns.Add(new("area_ha", "stockable forest area on the resource unit (in ha).", SqliteType.Real));
            //columns().Add(new("x_m", "x-coord", OutInteger)
            //columns().Add(new("y_m", "y-coord", OutInteger) // temp
            this.Columns.Add(new("count_ha", "tree count (living, >4m height) per ha", SqliteType.Integer));
            this.Columns.Add(new("dbh_avg_cm", "average dbh (cm)", SqliteType.Real));
            this.Columns.Add(new("height_avg_m", "average tree height (m)", SqliteType.Real));
            this.Columns.Add(new("volume_m3", "volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new("total_carbon_kg", "total carbon in living biomass (aboveground compartments and roots) of all living trees (including regeneration layer) (kg/ha)", SqliteType.Real));
            this.Columns.Add(new("gwl_m3", "'gesamtwuchsleistung' (total growth including removed/dead trees) volume (geomery, taper factor) in m³", SqliteType.Real));
            this.Columns.Add(new("basal_area_m2", "total basal area at breast height (m²)", SqliteType.Real));
            this.Columns.Add(new("NPP_kg", "sum of NPP (aboveground + belowground) kg biomass/ha", SqliteType.Real));
            this.Columns.Add(new("NPPabove_kg", "sum of NPP (abovegroundground) kg biomass/ha", SqliteType.Real));
            this.Columns.Add(new("LAI", "leaf area index (m²/m²)", SqliteType.Real));
            this.Columns.Add(new("cohort_count_ha", "number of cohorts in the regeneration layer (<4m) per hectare", SqliteType.Integer));
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            this.yearFilter.SetExpression(projectFile.Output.Sql.Stand.Condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (this.yearFilter.IsEmpty == false)
            {
                if (this.yearFilter.Evaluate(model.SimulationState.CurrentCalendarYear) == 0.0F)
                {
                    return;
                }
            }

            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                foreach (ResourceUnitTreeSpecies ruSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
                {
                    LiveTreeAndSaplingStatistics ruLiveTreeStatisticsForSpecies = ruSpecies.StatisticsLive;
                    if ((ruLiveTreeStatisticsForSpecies.TreesPerHa == 0.0F) && (ruLiveTreeStatisticsForSpecies.SaplingCohortsPerHa == 0.0F))
                    {
                        continue;
                    }

                    insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                    insertRow.Parameters[1].Value = resourceUnit.ID;
                    insertRow.Parameters[2].Value = ruSpecies.Species.WorldFloraID;
                    insertRow.Parameters[3].Value = resourceUnit.AreaInLandscapeInM2 / Constant.Grid.ResourceUnitAreaInM2;
                    // insertRow.Parameters[3].Value = ru.boundingBox().center().x() << ru.boundingBox().center().y();  // temp
                    insertRow.Parameters[4].Value = ruLiveTreeStatisticsForSpecies.TreesPerHa;
                    insertRow.Parameters[5].Value = ruLiveTreeStatisticsForSpecies.AverageDbhInCm;
                    insertRow.Parameters[6].Value = ruLiveTreeStatisticsForSpecies.AverageHeightInM;
                    insertRow.Parameters[7].Value = ruLiveTreeStatisticsForSpecies.StemVolumeInM3PerHa;
                    insertRow.Parameters[8].Value = ruLiveTreeStatisticsForSpecies.GetTotalCarbon();
                    float totalStemVolumeInM3PerHa = ruLiveTreeStatisticsForSpecies.StemVolumeInM3PerHa + ruSpecies.StatisticsManagement.StemVolumeInM3PerHa + ruSpecies.StatisticsSnag.StemVolumeInM3PerHa;
                    insertRow.Parameters[9].Value = totalStemVolumeInM3PerHa;
                    insertRow.Parameters[10].Value = ruLiveTreeStatisticsForSpecies.BasalAreaInM2PerHa;
                    insertRow.Parameters[11].Value = ruLiveTreeStatisticsForSpecies.TreeNppPerHa;
                    insertRow.Parameters[12].Value = ruLiveTreeStatisticsForSpecies.TreeNppPerHaAboveground;
                    insertRow.Parameters[13].Value = ruLiveTreeStatisticsForSpecies.LeafAreaIndex;
                    insertRow.Parameters[14].Value = ruLiveTreeStatisticsForSpecies.SaplingCohortsPerHa;
                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
