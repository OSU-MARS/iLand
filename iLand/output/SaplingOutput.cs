using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    internal class SaplingOutput : Output
    {
        private readonly Expression mFilter;

        public SaplingOutput()
        {
            this.mFilter = new Expression();

            Name = "Sapling Output";
            TableName = "sapling";
            Description = "Output of the establishment/sapling layer per resource unit and species." + System.Environment.NewLine +
                          "The output covers trees between a dbh of 1cm and the recruitment threshold (i.e. a height of 4m)." +
                          "Cohorts with a dbh < 1cm are counted in 'cohort_count_ha' but not used for average calculations." + System.Environment.NewLine + System.Environment.NewLine +
                          "You can specify a 'condition' to limit execution for specific time/ area with the variables 'ru' (resource unit id) and 'year' (the current year)";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(SqlColumn.CreateSpecies());
            Columns.Add(new SqlColumn("count_ha", "number of represented individuals per ha (tree height >1.3m).", OutputDatatype.OutInteger));
            Columns.Add(new SqlColumn("count_small_ha", "number of represented individuals per ha (with height <=1.3m).", OutputDatatype.OutInteger));
            Columns.Add(new SqlColumn("cohort_count_ha", "number of cohorts per ha.", OutputDatatype.OutInteger));
            Columns.Add(new SqlColumn("height_avg_m", "arithmetic average height of the cohorts (m) ", OutputDatatype.OutDouble));
            Columns.Add(new SqlColumn("age_avg", "arithmetic average age of the sapling cohorts (years)", OutputDatatype.OutDouble));
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

            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                if (ru.ID == -1)
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

                foreach (ResourceUnitSpecies rus in ru.Species)
                {
                    StandStatistics stat = rus.Statistics;
                    SaplingStat sap = rus.SaplingStats;

                    if (stat.SaplingCount == 0)
                    {
                        continue;
                    }
                    this.Add(CurrentYear());
                    this.Add(ru.Index);
                    this.Add(ru.ID);
                    this.Add(rus.Species.ID); // keys

                    // calculate statistics based on the number of represented trees per cohort
                    // double n = sap.livingStemNumber(rus.species(), out double avg_dbh, out double avg_height, out double avg_age);
                    this.Add(sap.LivingSaplings);
                    this.Add(sap.LivingSaplingsSmall);
                    this.Add(sap.LivingCohorts);
                    this.Add(sap.AverageHeight);
                    this.Add(sap.AverageAge);
                    this.WriteRow(insertRow);
                }
            }
        }
    }
}
