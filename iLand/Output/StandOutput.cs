using iLand.Simulation;
using iLand.Tools;
using iLand.Trees;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    /** StandOut is basic stand level info per species and ressource unit */
    public class StandOutput : Output
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
            Columns.Add(new SqlColumn("area_ha", "stockable forest area on the resource unit (in ha).", OutputDatatype.Double));
            //columns().Add(new OutputColumn("x_m", "x-coord", OutInteger)
            //columns().Add(new OutputColumn("y_m", "y-coord", OutInteger) // temp
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
            mFilter.SetExpression(model.Project.Output.Stand.Condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (!mFilter.IsEmpty)
            {
                if (mFilter.Evaluate(model, model.ModelSettings.CurrentYear) == 0.0)
                {
                    return;
                }
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
                    if (stat.Count == 0 && stat.CohortCount == 0)
                    {
                        continue;
                    }
                    insertRow.Parameters[0].Value = model.ModelSettings.CurrentYear;
                    insertRow.Parameters[1].Value = ru.Index;
                    insertRow.Parameters[2].Value = ru.ID;
                    insertRow.Parameters[3].Value = rus.Species.ID;
                    insertRow.Parameters[4].Value = ru.StockableArea / Constant.RUArea; // keys
                    // insertRow.Parameters[4].Value = ru.boundingBox().center().x() << ru.boundingBox().center().y();  // temp
                    insertRow.Parameters[5].Value = stat.Count;
                    insertRow.Parameters[6].Value = stat.AverageDbh;
                    insertRow.Parameters[7].Value = stat.AverageHeight;
                    insertRow.Parameters[8].Value = stat.StemVolume;
                    insertRow.Parameters[9].Value = stat.TotalCarbon();
                    insertRow.Parameters[10].Value = stat.TotalStemGrowth;
                    insertRow.Parameters[11].Value = stat.BasalArea;
                    insertRow.Parameters[12].Value = stat.Npp;
                    insertRow.Parameters[13].Value = stat.NppAbove;
                    insertRow.Parameters[14].Value = stat.LeafAreaIndex;
                    insertRow.Parameters[15].Value = stat.CohortCount;
                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
