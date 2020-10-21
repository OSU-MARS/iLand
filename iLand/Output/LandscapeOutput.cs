using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace iLand.Output
{
    /** LandscapeOut is aggregated output for the total landscape per species. All values are per hectare values. */
    public class LandscapeOutput : Output
    {
        private readonly Expression filter;
        private readonly Dictionary<string, StandStatistics> standStatisticsBySpecies;

        public LandscapeOutput()
        {
            this.filter = new Expression();
            this.standStatisticsBySpecies = new Dictionary<string, StandStatistics>();

            Name = "Landscape aggregates per species";
            TableName = "landscape";
            Description = "Output of aggregates on the level of landscape x species. Values are always aggregated per hectare." +
                          "The output is created after the growth of the year," +
                          "i.e. output with year=2000 means effectively the state of at the end of the" +
                          "year 2000.0 The initial state (without any growth) is indicated by the year 'startyear-1'." +
                          "You can use the 'condition' to control if the output should be created for the current year(see also dynamic stand output)";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateSpecies());
            Columns.Add(new SqlColumn("count_ha", "tree count (living, >4m height) per ha", OutputDatatype.Integer));
            Columns.Add(new SqlColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("height_avg_m", "average tree height (m)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.Double));
            Columns.Add(new SqlColumn("total_carbon_kg", "total carbon in living biomass (aboveground compartments and roots) of all living trees (including regeneration layer) (kg/ha)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("gwl_m3", "'gesamtwuchsleistung' (total growth including removed/dead trees) volume (geomery, taper factor) in m3", OutputDatatype.Double));
            Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", OutputDatatype.Double));
            Columns.Add(new SqlColumn("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", OutputDatatype.Double));
            Columns.Add(new SqlColumn("LAI", "Leafareaindex (m2/m2)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("cohort_count_ha", "number of cohorts in the regeneration layer (<4m) /ha", OutputDatatype.Integer));
        }

        public override void Setup(Model model)
        {
            // use a condition for to control execuation for the current year
            filter.SetExpression(model.Project.Output.Landscape.Condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (!filter.IsEmpty)
            {
                if (filter.Evaluate(model, model.ModelSettings.CurrentYear) == 0.0)
                {
                    return;
                }
            }

            // clear landscape stats
            foreach (KeyValuePair<string, StandStatistics> speciesStatistics in standStatisticsBySpecies)
            {
                speciesStatistics.Value.Clear();
            }

            // extract total stockable area
            double totalStockableArea = 0.0;
            foreach (ResourceUnit ru in model.ResourceUnits)
            {
                totalStockableArea += ru.StockableArea;
            }

            if (totalStockableArea == 0.0)
            {
                return;
            }

            foreach (ResourceUnit ru in model.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.Species)
                {
                    StandStatistics stat = rus.Statistics;
                    if (stat.Count == 0.0 && stat.CohortCount == 0 && stat.TotalStemGrowth == 0.0)
                    {
                        continue;
                    }
                    if (standStatisticsBySpecies.TryGetValue(rus.Species.ID, out StandStatistics statistics) == false)
                    {
                        statistics = new StandStatistics();
                        standStatisticsBySpecies.Add(rus.Species.ID, statistics);
                    }
                    statistics.AddAreaWeighted(stat, ru.StockableArea / totalStockableArea);
                }
            }

            // now add to output stream
            foreach (KeyValuePair<string, StandStatistics> species in this.standStatisticsBySpecies)
            {
                StandStatistics stat = species.Value;
                insertRow.Parameters[0].Value = model.ModelSettings.CurrentYear;
                insertRow.Parameters[1].Value = species.Key; // keys: year, species
                insertRow.Parameters[2].Value = stat.Count;
                insertRow.Parameters[3].Value = stat.AverageDbh;
                insertRow.Parameters[4].Value = stat.AverageHeight;
                insertRow.Parameters[5].Value = stat.StemVolume;
                insertRow.Parameters[6].Value = stat.TotalCarbon();
                insertRow.Parameters[7].Value = stat.TotalStemGrowth;
                insertRow.Parameters[8].Value = stat.BasalArea;
                insertRow.Parameters[9].Value = stat.Npp;
                insertRow.Parameters[10].Value = stat.NppAbove;
                insertRow.Parameters[11].Value = stat.LeafAreaIndex;
                insertRow.Parameters[12].Value = stat.CohortCount;
                insertRow.ExecuteNonQuery();
            }
        }
    }
}
