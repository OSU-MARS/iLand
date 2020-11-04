﻿using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class SaplingOutput : Output
    {
        private readonly Expression mFilter;

        public SaplingOutput()
        {
            this.mFilter = new Expression();

            this.Name = "Sapling Output";
            this.TableName = "sapling";
            this.Description = "Output of the establishment/sapling layer per resource unit and species." + System.Environment.NewLine +
                               "The output covers trees between a dbh of 1cm and the recruitment threshold (i.e. a height of 4m)." +
                               "Cohorts with a dbh < 1cm are counted in 'cohort_count_ha' but not used for average calculations." + System.Environment.NewLine + System.Environment.NewLine +
                               "You can specify a 'condition' to limit execution for specific time/ area with the variables 'ru' (resource unit id) and 'year' (the current year)";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new SqlColumn("count_ha", "number of represented individuals per ha (tree height >1.3m).", OutputDatatype.Integer));
            this.Columns.Add(new SqlColumn("count_small_ha", "number of represented individuals per ha (with height <=1.3m).", OutputDatatype.Integer));
            this.Columns.Add(new SqlColumn("cohort_count_ha", "number of cohorts per ha.", OutputDatatype.Integer));
            this.Columns.Add(new SqlColumn("height_avg_m", "arithmetic average height of the cohorts (m) ", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("age_avg", "arithmetic average age of the sapling cohorts (years)", OutputDatatype.Double));
        }

        public override void Setup(Model model)
        {
            this.mFilter.SetExpression(model.Project.Output.Sapling.Condition);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }

                if (!mFilter.IsEmpty)
                {
                    if (mFilter.Execute() == 0.0)
                    {
                        continue;
                    }
                }

                foreach (ResourceUnitSpecies rus in ru.TreeSpecies)
                {
                    ResourceUnitSpeciesStatistics stat = rus.Statistics;
                    SaplingProperties sap = rus.SaplingStats;

                    if (stat.SaplingCount == 0)
                    {
                        continue;
                    }
                    insertRow.Parameters[0].Value = model.CurrentYear;
                    insertRow.Parameters[1].Value = ru.GridIndex;
                    insertRow.Parameters[2].Value = ru.EnvironmentID;
                    insertRow.Parameters[3].Value = rus.Species.ID; // keys

                    // calculate statistics based on the number of represented trees per cohort
                    // double n = sap.livingStemNumber(rus.species(), out double avg_dbh, out double avg_height, out double avg_age;
                    insertRow.Parameters[4].Value = sap.LivingSaplings;
                    insertRow.Parameters[5].Value = sap.LivingSaplingsSmall;
                    insertRow.Parameters[6].Value = sap.LivingCohorts;
                    insertRow.Parameters[7].Value = sap.AverageHeight;
                    insertRow.Parameters[8].Value = sap.AverageAge;
                    insertRow.ExecuteNonQuery();
                }
            }
        }
    }
}
