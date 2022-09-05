using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    public class SaplingAnnualOutput : AnnualOutput
    {
        private readonly Expression resourceUnitFilter;

        public SaplingAnnualOutput()
        {
            this.resourceUnitFilter = new();

            this.Name = "Sapling Output";
            this.TableName = "sapling";
            this.Description = "Output of the establishment/sapling layer per resource unit and species." + System.Environment.NewLine +
                               "The output covers trees between a dbh of 1cm and the recruitment threshold (i.e. a height of 4m)." +
                               "Cohorts with a dbh < 1cm are counted in 'cohort_count_ha' but not used for average calculations." + System.Environment.NewLine + System.Environment.NewLine +
                               "You can specify a 'condition' to limit execution for specific time/ area with the variables 'ru' (resource unit id) and 'year' (the current year)";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(SqlColumn.CreateTreeSpeciesID());
            this.Columns.Add(new("count_ha", "number of represented individuals per ha (tree height >1.3m).", SqliteType.Integer));
            this.Columns.Add(new("count_small_ha", "number of represented individuals per ha (with height <=1.3m).", SqliteType.Integer));
            this.Columns.Add(new("cohort_count_ha", "number of cohorts per ha.", SqliteType.Integer));
            this.Columns.Add(new("height_avg_m", "arithmetic average height of the cohorts (m) ", SqliteType.Real));
            this.Columns.Add(new("age_avg", "arithmetic average age of the sapling cohorts (years)", SqliteType.Real));
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            this.resourceUnitFilter.SetExpression(projectFile.Output.Sql.Sapling.Condition);
            this.resourceUnitFilter.Wrapper = new ResourceUnitVariableAccessor(simulationState);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                if (this.resourceUnitFilter.IsEmpty == false)
                {
                    Debug.Assert(this.resourceUnitFilter.Wrapper != null);
                    ((ResourceUnitVariableAccessor)this.resourceUnitFilter.Wrapper).ResourceUnit = resourceUnit;
                    if (this.resourceUnitFilter.Execute() == 0.0F)
                    {
                        continue;
                    }
                }

                foreach (ResourceUnitTreeSpecies ruSpecies in resourceUnit.Trees.SpeciesAvailableOnResourceUnit)
                {
                    LiveTreeAndSaplingStatistics ruLiveTreeStatisticsForSpecies = ruSpecies.StatisticsLive;
                    if (ruLiveTreeStatisticsForSpecies.SaplingsPerHa == 0)
                    {
                        continue;
                    }

                    SaplingStatistics saplingStatisticsForSpecies = ruSpecies.SaplingStats;
                    insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                    insertRow.Parameters[1].Value = resourceUnit.ID;
                    insertRow.Parameters[2].Value = ruSpecies.Species.WorldFloraID; // keys

                    // calculate statistics based on the number of represented trees per cohort
                    // float n = sap.livingStemNumber(rus.species(), out float avg_dbh, out float avg_height, out float avg_age;
                    insertRow.Parameters[3].Value = saplingStatisticsForSpecies.LivingSaplings;
                    insertRow.Parameters[4].Value = saplingStatisticsForSpecies.LivingSaplingsSmall;
                    insertRow.Parameters[5].Value = saplingStatisticsForSpecies.LivingCohorts;
                    insertRow.Parameters[6].Value = saplingStatisticsForSpecies.AverageHeight;
                    insertRow.Parameters[7].Value = saplingStatisticsForSpecies.AverageAgeInYears;
                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
