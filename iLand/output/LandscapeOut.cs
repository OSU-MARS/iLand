using iLand.core;
using iLand.tools;
using System.Collections.Generic;

namespace iLand.output
{
    /** LandscapeOut is aggregated output for the total landscape per species. All values are per hectare values. */
    internal class LandscapeOut : Output
    {
        private readonly Expression mCondition;
        private readonly Dictionary<string, StandStatistics> mLandscapeStats;

        public LandscapeOut()
        {
            this.mCondition = new Expression();
            this.mLandscapeStats = new Dictionary<string, StandStatistics>();

            Name = "Landscape aggregates per species";
            TableName = "landscape";
            Description = "Output of aggregates on the level of landscape x species. Values are always aggregated per hectare." +
                          "The output is created after the growth of the year," +
                          "i.e. output with year=2000 means effectively the state of at the end of the" +
                          "year 2000.0 The initial state (without any growth) is indicated by the year 'startyear-1'." +
                          "You can use the 'condition' to control if the output should be created for the current year(see also dynamic stand output)";
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateSpecies());
            Columns.Add(new OutputColumn("count_ha", "tree count (living, >4m height) per ha", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("height_avg_m", "average tree height (m)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("total_carbon_kg", "total carbon in living biomass (aboveground compartments and roots) of all living trees (including regeneration layer) (kg/ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("gwl_m3", "'gesamtwuchsleistung' (total growth including removed/dead trees) volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("LAI", "Leafareaindex (m2/m2)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("cohort_count_ha", "number of cohorts in the regeneration layer (<4m) /ha", OutputDatatype.OutInteger));
        }

        public override void Setup()
        {
            // use a condition for to control execuation for the current year
            string condition = Settings().Value(".condition", "");
            mCondition.SetExpression(condition);
        }

        public override void Exec()
        {
            Model m = GlobalSettings.Instance.Model;
            if (!mCondition.IsEmpty)
            {
                if (mCondition.Calculate(GlobalSettings.Instance.CurrentYear) == 0.0)
                {
                    return;
                }
            }

            // clear landscape stats
            foreach (KeyValuePair<string, StandStatistics> i in mLandscapeStats)
            {
                i.Value.Clear();
            }

            // extract total stockable area
            double total_area = 0.0;
            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                total_area += ru.StockableArea;
            }

            if (total_area == 0.0)
            {
                return;
            }

            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitSpecies rus in ru.Species)
                {
                    StandStatistics stat = rus.Statistics;
                    if (stat.Count == 0.0 && stat.CohortCount == 0 && stat.Gwl == 0.0)
                    {
                        continue;
                    }
                    mLandscapeStats[rus.Species.ID].AddAreaWeighted(stat, ru.StockableArea / total_area);
                }
            }
            // now add to output stream
            foreach (KeyValuePair<string, StandStatistics> i in mLandscapeStats)
            {
                StandStatistics stat = i.Value;
                this.Add(CurrentYear());
                this.Add(i.Key); // keys: year, species
                this.Add(stat.Count);
                this.Add(stat.AverageDbh);
                this.Add(stat.AverageHeight);
                this.Add(stat.Volume);
                this.Add(stat.TotalCarbon());
                this.Add(stat.Gwl);
                this.Add(stat.BasalArea);
                this.Add(stat.Npp);
                this.Add(stat.NppAbove);
                this.Add(stat.LeafAreaIndex);
                this.Add(stat.CohortCount);
                WriteRow();
            }
        }
    }
}
