using iLand.core;
using iLand.tools;

namespace iLand.output
{
    internal class SaplingDetailsOut : Output
    {
        private readonly Expression mCondition;
        private double mMinDbh;

        public SaplingDetailsOut()
        {
            this.mCondition = new Expression();

            Name = "Sapling Details Output";
            TableName = "saplingdetail";
            Description = "Detailed output on indidvidual sapling cohorts." + System.Environment.NewLine +
                          "For each occupied and living 2x2m pixel, a row is generated, unless" +
                          "the tree diameter is below the 'minDbh' threshold (cm). " +
                          "You can further specify a 'condition' to limit execution for specific time/ area with the variables 'ru' (resource unit id) and 'year' (the current year).";
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateResourceUnit());
            Columns.Add(OutputColumn.CreateID());
            Columns.Add(OutputColumn.CreateSpecies());
            Columns.Add(new OutputColumn("n_represented", "number of trees that are represented by the cohort (Reineke function).", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("dbh", "diameter of the cohort (cm).", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("height", "height of the cohort (m).", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("age", "age of the cohort (years) ", OutputDatatype.OutInteger));
        }

        public override void Exec()
        {
            Model m = GlobalSettings.Instance.Model;

            foreach (ResourceUnit ru in m.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }

                // exclude if a condition is specified and condition is not met
                if (!mCondition.IsEmpty)
                {
                    if (mCondition.Execute() == 0.0)
                    {
                        continue;
                    }
                }
                SaplingCell[] saplingCells = ru.SaplingCells;
                for (int px = 0; px < Constant.LightCellsPerHectare; ++px)
                {
                    SaplingCell s = saplingCells[px];
                    int n_on_px = s.GetOccupiedSlotCount();
                    if (n_on_px > 0)
                    {
                        for (int i = 0; i < SaplingCell.SaplingCells; ++i)
                        {
                            if (s.Saplings[i].IsOccupied())
                            {
                                ResourceUnitSpecies rus = s.Saplings[i].ResourceUnitSpecies(ru);
                                Species species = rus.Species;
                                double dbh = s.Saplings[i].Height / species.SaplingGrowthParameters.HdSapling * 100.0;
                                // check minimum dbh
                                if (dbh < mMinDbh)
                                {
                                    continue;
                                }
                                double n_repr = species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(s.Saplings[i].Height) / n_on_px;

                                this.Add(CurrentYear());
                                this.Add(ru.Index);
                                this.Add(ru.ID);
                                this.Add(rus.Species.ID);
                                this.Add(n_repr);
                                this.Add(dbh);
                                this.Add(s.Saplings[i].Height);
                                this.Add(s.Saplings[i].Age);
                                WriteRow();
                            }
                        }
                    }
                }
            }
        }

        public override void Setup()
        {
            // use a condition for to control execuation for the current year
            string condition = Settings().Value(".condition", "");
            mCondition.SetExpression(condition);
            mMinDbh = Settings().ValueDouble(".minDbh");
        }
    }
}
