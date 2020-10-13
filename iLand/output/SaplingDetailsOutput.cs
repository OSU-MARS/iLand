using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class SaplingDetailsOutput : Output
    {
        private readonly Expression mFilter;
        private double mMinDbh;

        public SaplingDetailsOutput()
        {
            this.mFilter = new Expression();

            Name = "Sapling Details Output";
            TableName = "saplingdetail";
            Description = "Detailed output on indidvidual sapling cohorts." + System.Environment.NewLine +
                          "For each occupied and living 2x2m pixel, a row is generated, unless" +
                          "the tree diameter is below the 'minDbh' threshold (cm). " +
                          "You can further specify a 'condition' to limit execution for specific time/ area with the variables 'ru' (resource unit id) and 'year' (the current year).";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(SqlColumn.CreateSpecies());
            Columns.Add(new SqlColumn("n_represented", "number of trees that are represented by the cohort (Reineke function).", OutputDatatype.Double));
            Columns.Add(new SqlColumn("dbh", "diameter of the cohort (cm).", OutputDatatype.Double));
            Columns.Add(new SqlColumn("height", "height of the cohort (m).", OutputDatatype.Double));
            Columns.Add(new SqlColumn("age", "age of the cohort (years) ", OutputDatatype.Integer));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit ru in model.ResourceUnits)
            {
                if (ru.ID == -1)
                {
                    continue; // do not include if out of project area
                }

                // exclude if a condition is specified and condition is not met
                if (!mFilter.IsEmpty)
                {
                    if (mFilter.Execute(model) == 0.0)
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
                        for (int i = 0; i < SaplingCell.SaplingsPerCell; ++i)
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

                                this.Add(model.GlobalSettings.CurrentYear);
                                this.Add(ru.Index);
                                this.Add(ru.ID);
                                this.Add(rus.Species.ID);
                                this.Add(n_repr);
                                this.Add(dbh);
                                this.Add(s.Saplings[i].Height);
                                this.Add(s.Saplings[i].Age);
                                this.WriteRow(insertRow);
                            }
                        }
                    }
                }
            }
        }

        public override void Setup(GlobalSettings globalSettings)
        {
            // use a condition for to control execuation for the current year
            string condition = globalSettings.Settings.GetString(".condition", "");
            mFilter.SetExpression(condition);
            mMinDbh = globalSettings.Settings.GetDouble(".minDbh");
        }
    }
}
