using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    /** StandOut is basic stand level info per species and ressource unit */
    internal class StandOutput : Output
    {
        private readonly Expression mFilter;

        public StandOutput()
        {
            this.mFilter = new Expression();

            Name = "Stand by species/RU";
            TableName = "stand";
            Description = "Output of aggregates on the level of RU x species. Values are always aggregated per hectare (of stockable area). " +
                          "Use the 'area' column to scale to the actual values on the resource unit." + System.Environment.NewLine +
                          "The output is created after the growth of the year, " +
                          "i.e. output with year=2000 means effectively the state of at the end of the " +
                          "year 2000.0 The initial state (without any growth) is indicated by the year 'startyear-1'. " +
                          "You can use the 'condition' to control if the output should be created for the current year(see dynamic stand output)";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(SqlColumn.CreateSpecies());
            Columns.Add(new SqlColumn("area_ha", "stockable forest area on the resource unit (in ha).", OutputDatatype.OutDouble));
            //columns().Add(new OutputColumn("x_m", "x-coord", OutInteger)
            //columns().Add(new OutputColumn("y_m", "y-coord", OutInteger) // temp
            Columns.Add(new SqlColumn("count_ha", "tree count (living, >4m height) per ha", OutputDatatype.OutInteger));
            Columns.Add(new SqlColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("height_avg_m", "average tree height (m)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("total_carbon_kg", "total carbon in living biomass (aboveground compartments and roots) of all living trees (including regeneration layer) (kg/ha)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("gwl_m3", "'gesamtwuchsleistung' (total growth including removed/dead trees) volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("LAI", "Leafareaindex (m2/m2)", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("cohort_count_ha", "number of cohorts in the regeneration layer (<4m) /ha", OutputDatatype.OutInteger));
        }

        public override void Setup()
        {
            // use a condition for to control execuation for the current year
            string condition = Settings().GetString(".condition", "");
            mFilter.SetExpression(condition);
        }

        protected override void LogYear(SqliteCommand insertRow)
        {
            Model m = GlobalSettings.Instance.Model;
            if (!mFilter.IsEmpty)
            {
                if (mFilter.Calculate(GlobalSettings.Instance.CurrentYear) == 0.0)
                {
                    return;
                }
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
                    if (stat.Count == 0 && stat.CohortCount == 0)
                    {
                        continue;
                    }
                    this.Add(CurrentYear());
                    this.Add(ru.Index);
                    this.Add(ru.ID);
                    this.Add(rus.Species.ID);
                    this.Add(ru.StockableArea / Constant.RUArea); // keys
                    // this.add(ru.boundingBox().center().x() << ru.boundingBox().center().y());  // temp
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
                    this.WriteRow(insertRow);
                }
            }
        }
    }
}
