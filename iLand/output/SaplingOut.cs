using iLand.core;
using iLand.tools;

namespace iLand.output
{
    internal class SaplingOut : Output
    {
        private Expression mCondition;
        private double mVarRu;
        private double mVarYear;

        public SaplingOut()
        {

            setName("Sapling Output", "sapling");
            setDescription("Output of the establishment/sapling layer per resource unit and species." + System.Environment.NewLine +
                       "The output covers trees between a dbh of 1cm and the recruitment threshold (i.e. a height of 4m)." +
                       "Cohorts with a dbh < 1cm are counted in 'cohort_count_ha' but not used for average calculations." + System.Environment.NewLine + System.Environment.NewLine +
                       "You can specify a 'condition' to limit execution for specific time/ area with the variables 'ru' (resource unit id) and 'year' (the current year)");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(OutputColumn.species());
            columns().Add(new OutputColumn("count_ha", "number of represented individuals per ha (tree height >1.3m).", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("count_small_ha", "number of represented individuals per ha (with height <=1.3m).", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("cohort_count_ha", "number of cohorts per ha.", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("height_avg_m", "arithmetic average height of the cohorts (m) ", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("age_avg", "arithmetic average age of the sapling cohorts (years)", OutputDatatype.OutDouble));
        }

        public void setup()
        {
            // use a condition for to control execuation for the current year
            string condition = settings().value(".condition", "");
            mCondition.setExpression(condition);
            if (!mCondition.isEmpty())
            {
                mVarRu = mCondition.addVar("ru");
                mVarYear = mCondition.addVar("year");
            }
        }

        public override void exec()
        {
            Model m = GlobalSettings.instance().model();

            foreach (ResourceUnit ru in m.ruList())
            {
                if (ru.id() == -1)
                {
                    continue; // do not include if out of project area
                }

                if (!mCondition.isEmpty())
                {
                    mVarRu = ru.id();
                    mVarYear = GlobalSettings.instance().currentYear();
                    if (mCondition.execute() == 0.0)
                    {
                        continue;
                    }
                }

                foreach (ResourceUnitSpecies rus in ru.ruSpecies())
                {
                    StandStatistics stat = rus.constStatistics();
                    SaplingStat sap = rus.saplingStat();

                    if (stat.saplingCount() == 0)
                    {
                        continue;
                    }
                    this.add(currentYear());
                    this.add(ru.index());
                    this.add(ru.id());
                    this.add(rus.species().id()); // keys

                    // calculate statistics based on the number of represented trees per cohort
                    // double n = sap.livingStemNumber(rus.species(), out double avg_dbh, out double avg_height, out double avg_age);
                    this.add(sap.livingSaplings());
                    this.add(sap.livingSaplingsSmall());
                    this.add(sap.livingCohorts());
                    this.add(sap.averageHeight());
                    this.add(sap.averageAge());
                    writeRow();
                }
            }
        }
    }
}
