using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
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
            this.Columns.Add(new SqlColumn("area_ha", "stockable forest area on the resource unit (in ha).", OutputDatatype.Double));
            //columns().Add(new OutputColumn("x_m", "x-coord", OutInteger)
            //columns().Add(new OutputColumn("y_m", "y-coord", OutInteger) // temp
            this.Columns.Add(new SqlColumn("count_ha", "tree count (living, >4m height) per ha", OutputDatatype.Integer));
            this.Columns.Add(new SqlColumn("dbh_avg_cm", "average dbh (cm)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("height_avg_m", "average tree height (m)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("volume_m3", "volume (geomery, taper factor) in m3", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("total_carbon_kg", "total carbon in living biomass (aboveground compartments and roots) of all living trees (including regeneration layer) (kg/ha)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("gwl_m3", "'gesamtwuchsleistung' (total growth including removed/dead trees) volume (geomery, taper factor) in m3", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("NPP_kg", "sum of NPP (aboveground + belowground) kg Biomass/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("NPPabove_kg", "sum of NPP (abovegroundground) kg Biomass/ha", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("LAI", "Leafareaindex (m2/m2)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("cohort_count_ha", "number of cohorts in the regeneration layer (<4m) /ha", OutputDatatype.Integer));
        }

        public override void Setup(Model model)
        {
            this.mFilter.SetExpression(model.Project.Output.Stand.Condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            if (!mFilter.IsEmpty)
            {
                if (mFilter.Evaluate(model.CurrentYear) == 0.0)
                {
                    return;
                }
            }

            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }
                foreach (ResourceUnitTreeSpecies ruSpecies in ru.Trees.SpeciesAvailableOnResourceUnit)
                {
                    ResourceUnitTreeStatistics speciesStats = ruSpecies.Statistics;
                    if (speciesStats.TreesPerHectare == 0 && speciesStats.CohortCount == 0)
                    {
                        continue;
                    }
                    insertRow.Parameters[0].Value = model.CurrentYear;
                    insertRow.Parameters[1].Value = ru.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = ru.EnvironmentID;
                    insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                    insertRow.Parameters[4].Value = ru.AreaInLandscape / Constant.RUArea; // keys
                    // insertRow.Parameters[4].Value = ru.boundingBox().center().x() << ru.boundingBox().center().y();  // temp
                    insertRow.Parameters[5].Value = speciesStats.TreesPerHectare;
                    insertRow.Parameters[6].Value = speciesStats.AverageDbh;
                    insertRow.Parameters[7].Value = speciesStats.AverageHeight;
                    insertRow.Parameters[8].Value = speciesStats.StemVolume;
                    insertRow.Parameters[9].Value = speciesStats.GetTotalCarbon();
                    insertRow.Parameters[10].Value = speciesStats.TotalStemVolumeGrowth;
                    insertRow.Parameters[11].Value = speciesStats.BasalArea;
                    insertRow.Parameters[12].Value = speciesStats.Npp;
                    insertRow.Parameters[13].Value = speciesStats.NppAbove;
                    insertRow.Parameters[14].Value = speciesStats.LeafAreaIndex;
                    insertRow.Parameters[15].Value = speciesStats.CohortCount;
                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
