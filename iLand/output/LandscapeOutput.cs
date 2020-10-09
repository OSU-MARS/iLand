using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace iLand.Output
{
    /** LandscapeOut is aggregated output for the total landscape per species. All values are per hectare values. */
    internal class LandscapeOutput : Output
    {
        private readonly Expression mFilter;
        private readonly Dictionary<string, StandStatistics> mStandStatisticsBySpecies;

        public LandscapeOutput()
        {
            this.mFilter = new Expression();
            this.mStandStatisticsBySpecies = new Dictionary<string, StandStatistics>();

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

        public override void Setup(GlobalSettings globalSettings)
        {
            // use a condition for to control execuation for the current year
            string condition = globalSettings.Settings.GetString(".condition", "");
            mFilter.SetExpression(condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (!mFilter.IsEmpty)
            {
                if (mFilter.Calculate(model.GlobalSettings, model.GlobalSettings.CurrentYear) == 0.0)
                {
                    return;
                }
            }

            // clear landscape stats
            foreach (KeyValuePair<string, StandStatistics> speciesStatistics in mStandStatisticsBySpecies)
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
                    if (mStandStatisticsBySpecies.TryGetValue(rus.Species.ID, out StandStatistics statistics) == false)
                    {
                        statistics = new StandStatistics();
                        mStandStatisticsBySpecies.Add(rus.Species.ID, statistics);
                    }
                    statistics.AddAreaWeighted(stat, ru.StockableArea / totalStockableArea);
                }
            }

            // now add to output stream
            foreach (KeyValuePair<string, StandStatistics> i in mStandStatisticsBySpecies)
            {
                StandStatistics stat = i.Value;
                this.Add(model.GlobalSettings.CurrentYear);
                this.Add(i.Key); // keys: year, species
                this.Add(stat.Count);
                this.Add(stat.AverageDbh);
                this.Add(stat.AverageHeight);
                this.Add(stat.StemVolume);
                this.Add(stat.TotalCarbon());
                this.Add(stat.TotalStemGrowth);
                this.Add(stat.BasalArea);
                this.Add(stat.Npp);
                this.Add(stat.NppAbove);
                this.Add(stat.LeafAreaIndex);
                this.Add(stat.CohortCount);
                this.WriteRow(insertRow);
            }
        }
    }
}
