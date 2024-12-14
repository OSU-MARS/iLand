// C++/output/{ landscapeout.h, landscapeout.cpp }
using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    /** LandscapeOut is aggregated output for the total landscape per species. All values are per hectare values. */
    public class LandscapeTreeSpeciesAnnualOutput : AnnualOutput
    {
        private readonly Expression filter;
        private readonly SortedList<WorldFloraID, LandscapeTreeSpeciesStatistics> treeStatisticsBySpecies;

        public LandscapeTreeSpeciesAnnualOutput()
        {
            this.filter = new();
            this.treeStatisticsBySpecies = [];

            this.Name = "Landscape aggregates per species";
            this.TableName = "landscape";
            this.Description = "Output of aggregates on the level of landscape x species. Values are always aggregated per hectare." +
                               "The output is created after the growth of the year," +
                               "i.e. output with year=2000 means effectively the state of at the end of the" +
                               "year 2000.0 The initial state (without any growth) is indicated by the year 'startyear-1'." +
                               "You can use the 'condition' to control if the output should be created for the current year(see also dynamic stand output)";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(new("area_stockable_ha", "Total stockable area of the simulated landscape, ha (10 x 10 m cell resolution).", SqliteType.Real));
            this.Columns.Add(new("area_ru_ha", "Total area of all simulated resource units, ha (1 ha resolution), which is the same as the number of resource units simulated. This area is larger than 'area' if any resource unit is only partially stockable.", SqliteType.Integer));
            this.Columns.Add(SqlColumn.CreateTreeSpeciesID());
            this.Columns.Add(new("count_ha", "Tree count (living, >4m height) per ha", SqliteType.Integer));
            this.Columns.Add(new("dbh_avg_cm", "Average DBH, cm.", SqliteType.Real));
            this.Columns.Add(new("height_avg_m", "Average tree height, m.", SqliteType.Real));
            this.Columns.Add(new("volume_m3", "Volume (geomery, taper factor) in m³.", SqliteType.Real));
            this.Columns.Add(new("total_carbon_kg", "Total carbon in living biomass (aboveground compartments and roots) of all living trees (including regeneration layer) (kg/ha).", SqliteType.Real));
            this.Columns.Add(new("gwl_m3", "'Gesamtwuchsleistung' (total growth including removed/dead trees) volume (geomery, taper factor) in m³.", SqliteType.Real));
            this.Columns.Add(new("basal_area_m2", "Total basal area at breast height (m²).", SqliteType.Real));
            this.Columns.Add(new("NPP_kg", "Sum of NPP (aboveground + belowground) kg biomass/ha.", SqliteType.Real));
            this.Columns.Add(new("NPPabove_kg", "Sum of NPP (abovegroundground) kg biomass/ha.", SqliteType.Real));
            this.Columns.Add(new("LAI", "Leaf area index (m²/m²),", SqliteType.Real));
            this.Columns.Add(new("cohort_count_ha", "Number of cohorts in the regeneration layer (<4m) per hectare.", SqliteType.Integer));
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            // use a condition for to control execuation for the current year
            this.filter.SetExpression(projectFile.Output.Sql.Landscape.Condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (this.filter.IsEmpty == false)
            {
                int currentCalendarYear = model.SimulationState.CurrentCalendarYear;
                if (this.filter.Evaluate(currentCalendarYear) == 0.0F)
                {
                    return;
                }
            }

            // clear landscape stats
            for (int treeSpeciesIndex = 0; treeSpeciesIndex < this.treeStatisticsBySpecies.Count; ++treeSpeciesIndex)
            {
                this.treeStatisticsBySpecies.Values[treeSpeciesIndex].Zero();
            }


            float totalLandscapeAreaInM2 = 0.0F;
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                totalLandscapeAreaInM2 += resourceUnit.AreaInLandscapeInM2;
                foreach (ResourceUnitTreeSpecies ruSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
                {
                    LiveTreeAndSaplingStatistics ruLiveTreeStatisticsForSpecies = ruSpecies.StatisticsLive;

                    // removed growth is the running sum of all removed
                    // tree volume. the current "GWL" therefore is current volume (standing) + mRemovedGrowth.
                    // important: statisticsDead() and statisticsMgmt() need to calculate() before -> volume() is already scaled to ha
                    float totalStemVolumeInM3PerHa = ruLiveTreeStatisticsForSpecies.StemVolumeInM3PerHa + ruSpecies.StatisticsManagement.StemVolumeInM3PerHa + ruSpecies.StatisticsSnag.StemVolumeInM3PerHa;
                    if ((ruLiveTreeStatisticsForSpecies.TreesPerHa == 0.0F) && (ruLiveTreeStatisticsForSpecies.SaplingCohortsPerHa == 0.0F) && (totalStemVolumeInM3PerHa == 0.0F))
                    {
                        continue;
                    }
                    if (this.treeStatisticsBySpecies.TryGetValue(ruSpecies.Species.WorldFloraID, out LandscapeTreeSpeciesStatistics? speciesStatistics) == false)
                    {
                        speciesStatistics = new();
                        this.treeStatisticsBySpecies.Add(ruSpecies.Species.WorldFloraID, speciesStatistics);
                    }
                    speciesStatistics.AddResourceUnit(resourceUnit, ruLiveTreeStatisticsForSpecies, totalStemVolumeInM3PerHa);
                }
            }

            // write species to output stream
            for (int treeSpeciesIndex = 0; treeSpeciesIndex < this.treeStatisticsBySpecies.Count; ++treeSpeciesIndex)
            {
                LandscapeTreeSpeciesStatistics speciesStats = this.treeStatisticsBySpecies.Values[treeSpeciesIndex];
                if (speciesStats.TreeCount > 0.0F)
                {
                    // species may have died out of landscape, in which case tree count and all other properties should be zero
                    speciesStats.ConvertIncrementalSumsToAreaWeightedAverages();
                }

                insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                insertRow.Parameters[1].Value = totalLandscapeAreaInM2 / Constant.Grid.ResourceUnitAreaInM2; // m² -> ha
                insertRow.Parameters[2].Value = model.Landscape.ResourceUnits.Count;
                insertRow.Parameters[3].Value = this.treeStatisticsBySpecies.Keys[treeSpeciesIndex];
                insertRow.Parameters[4].Value = speciesStats.TreeCount;
                insertRow.Parameters[5].Value = speciesStats.AverageDbh;
                insertRow.Parameters[6].Value = speciesStats.AverageHeight;
                insertRow.Parameters[7].Value = speciesStats.LiveStandingStemVolume;
                insertRow.Parameters[8].Value = speciesStats.TotalCarbon;
                insertRow.Parameters[9].Value = speciesStats.LiveStandingAndRemovedStemVolume;
                insertRow.Parameters[10].Value = speciesStats.BasalArea;
                insertRow.Parameters[11].Value = speciesStats.TreeNpp;
                insertRow.Parameters[12].Value = speciesStats.TreeNppAboveground;
                insertRow.Parameters[13].Value = speciesStats.LeafAreaIndex;
                insertRow.Parameters[14].Value = speciesStats.CohortCount;
                insertRow.ExecuteNonQuery();
            }
        }
    }
}
