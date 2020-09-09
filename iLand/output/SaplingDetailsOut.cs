using iLand.core;
using iLand.tools;

namespace iLand.output
{
    internal class SaplingDetailsOut : Output
    {
        private Expression mCondition;
        private double mVarRu;
        private double mVarYear;
        private double mMinDbh;

        public SaplingDetailsOut()
        {
            setName("Sapling Details Output", "saplingdetail");
            setDescription("Detailed output on indidvidual sapling cohorts." + System.Environment.NewLine +
                       "For each occupied and living 2x2m pixel, a row is generated, unless" +
                       "the tree diameter is below the 'minDbh' threshold (cm). " +
                       "You can further specify a 'condition' to limit execution for specific time/ area with the variables 'ru' (resource unit id) and 'year' (the current year).");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.ru());
            columns().Add(OutputColumn.id());
            columns().Add(OutputColumn.species());
            columns().Add(new OutputColumn("n_represented", "number of trees that are represented by the cohort (Reineke function).", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("dbh", "diameter of the cohort (cm).", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("height", "height of the cohort (m).", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("age", "age of the cohort (years) ", OutputDatatype.OutInteger));
        }

        public override void exec()
        {
            Model m = GlobalSettings.instance().model();

            foreach (ResourceUnit ru in m.ruList())
            {
                if (ru.id() == -1)
                    continue; // do not include if out of project area

                // exclude if a condition is specified and condition is not met
                if (!mCondition.isEmpty())
                {
                    mVarRu = ru.id();
                    mVarYear = GlobalSettings.instance().currentYear();
                    if (mCondition.execute() == 0.0)
                    {
                        continue;
                    }
                }
                SaplingCell[] saplingCells = ru.saplingCellArray();
                for (int px = 0; px < Constant.cPxPerHectare; ++px)
                {
                    SaplingCell s = saplingCells[px];
                    int n_on_px = s.n_occupied();
                    if (n_on_px > 0)
                    {
                        for (int i = 0; i < SaplingCell.NSAPCELLS; ++i)
                        {
                            if (s.saplings[i].is_occupied())
                            {
                                ResourceUnitSpecies rus = s.saplings[i].resourceUnitSpecies(ru);
                                Species species = rus.species();
                                double dbh = s.saplings[i].height / species.saplingGrowthParameters().hdSapling * 100.0;
                                // check minimum dbh
                                if (dbh < mMinDbh)
                                {
                                    continue;
                                }
                                double n_repr = species.saplingGrowthParameters().representedStemNumberH(s.saplings[i].height) / n_on_px;

                                this.add(currentYear());
                                this.add(ru.index());
                                this.add(ru.id());
                                this.add(rus.species().id());
                                this.add(n_repr);
                                this.add(dbh);
                                this.add(s.saplings[i].height);
                                this.add(s.saplings[i].age);
                                writeRow();
                            }
                        }
                    }
                }
            }
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
            mMinDbh = settings().valueDouble(".minDbh");
        }
    }
}
