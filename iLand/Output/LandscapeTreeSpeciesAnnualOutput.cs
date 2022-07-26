using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace iLand.Output
{
    /** LandscapeOut is aggregated output for the total landscape per species. All values are per hectare values. */
    public class LandscapeTreeSpeciesAnnualOutput : AnnualOutput
    {
        private readonly Expression filter;
        private readonly Dictionary<string, LandscapeTreeSpeciesStatistics> treeStatisticsBySpeciesID;

        public LandscapeTreeSpeciesAnnualOutput()
        {
            this.filter = new();
            this.treeStatisticsBySpeciesID = new();

            this.Name = "Landscape aggregates per species";
            this.TableName = "landscape";
            this.Description = "Output of aggregates on the level of landscape x species. Values are always aggregated per hectare." +
                               "The output is created after the growth of the year," +
                               "i.e. output with year=2000 means effectively the state of at the end of the" +
                               "year 2000.0 The initial state (without any growth) is indicated by the year 'startyear-1'." +
                               "You can use the 'condition' to control if the output should be created for the current year(see also dynamic stand output)";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new SqlColumn("count_ha", "tree count (living, >4m height) per ha", SqliteType.Integer));
            this.Columns.Add(new SqlColumn("dbh_avg_cm", "average dbh (cm)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("height_avg_m", "average tree height (m)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("volume_m3", "volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new SqlColumn("total_carbon_kg", "total carbon in living biomass (aboveground compartments and roots) of all living trees (including regeneration layer) (kg/ha)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("gwl_m3", "'gesamtwuchsleistung' (total growth including removed/dead trees) volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", SqliteType.Real));
            this.Columns.Add(new SqlColumn("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", SqliteType.Real));
            this.Columns.Add(new SqlColumn("LAI", "Leafareaindex (m2/m2)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("cohort_count_ha", "number of cohorts in the regeneration layer (<4m) /ha", SqliteType.Integer));
        }

        public override void Setup(Model model)
        {
            // use a condition for to control execuation for the current year
            this.filter.SetExpression(model.Project.Output.Annual.Landscape.Condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (this.filter.IsEmpty == false)
            {
                if (this.filter.Evaluate(model.CurrentYear) == 0.0)
                {
                    return;
                }
            }

            // clear landscape stats
            foreach ((string _, LandscapeTreeSpeciesStatistics speciesStatistics) in this.treeStatisticsBySpeciesID)
            {
                speciesStatistics.Zero();
            }

            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                foreach (ResourceUnitTreeSpecies ruSpecies in ru.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeStatistics ruSpeciesStats = ruSpecies.StatisticsLive;
                    if (ruSpeciesStats.TreeCount == 0 && ruSpeciesStats.CohortCount == 0 && ruSpeciesStats.LiveAndSnagStemVolume == 0.0F)
                    {
                        continue;
                    }
                    if (this.treeStatisticsBySpeciesID.TryGetValue(ruSpecies.Species.ID, out LandscapeTreeSpeciesStatistics? speciesStatistics) == false)
                    {
                        speciesStatistics = new LandscapeTreeSpeciesStatistics();
                        this.treeStatisticsBySpeciesID.Add(ruSpecies.Species.ID, speciesStatistics);
                    }
                    speciesStatistics.AddResourceUnit(ru, ruSpeciesStats);
                }
            }

            // write species to output stream
            foreach (KeyValuePair<string, LandscapeTreeSpeciesStatistics> species in this.treeStatisticsBySpeciesID)
            {
                LandscapeTreeSpeciesStatistics speciesStats = species.Value;
                speciesStats.ConvertSumsToAreaWeightedAverages();

                insertRow.Parameters[0].Value = model.CurrentYear;
                insertRow.Parameters[1].Value = species.Key; // keys: year, species
                insertRow.Parameters[2].Value = speciesStats.TreeCount;
                insertRow.Parameters[3].Value = speciesStats.AverageDbh;
                insertRow.Parameters[4].Value = speciesStats.AverageHeight;
                insertRow.Parameters[5].Value = speciesStats.LiveStemVolume;
                insertRow.Parameters[6].Value = speciesStats.TotalCarbon;
                insertRow.Parameters[7].Value = speciesStats.LiveAndSnagStemVolume;
                insertRow.Parameters[8].Value = speciesStats.BasalArea;
                insertRow.Parameters[9].Value = speciesStats.TreeNpp;
                insertRow.Parameters[10].Value = speciesStats.TreeNppAboveground;
                insertRow.Parameters[11].Value = speciesStats.LeafAreaIndex;
                insertRow.Parameters[12].Value = speciesStats.CohortCount;
                insertRow.ExecuteNonQuery();
            }
        }
    }
}
