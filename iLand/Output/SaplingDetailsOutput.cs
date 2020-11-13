using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class SaplingDetailsOutput : Output
    {
        private readonly Expression mFilter;
        private float mMinDbh;

        public SaplingDetailsOutput()
        {
            this.mFilter = new Expression();

            this.Name = "Sapling Details Output";
            this.TableName = "saplingDetail";
            this.Description = "Detailed output on indidvidual sapling cohorts." + System.Environment.NewLine +
                               "For each occupied and living 2x2m pixel, a row is generated, unless" +
                               "the tree diameter is below the 'minDbh' threshold (cm). " +
                               "You can further specify a 'condition' to limit execution for specific time/ area with the variables 'ru' (resource unit id) and 'year' (the current year).";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new SqlColumn("n_represented", "number of trees that are represented by the cohort (Reineke function).", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("dbh", "diameter of the cohort (cm).", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("height", "height of the cohort (m).", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("age", "age of the cohort (years) ", OutputDatatype.Integer));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit ru in model.Landscape.ResourceUnits)
            {
                if (ru.EnvironmentID == -1)
                {
                    continue; // do not include if out of project area
                }

                // exclude if a condition is specified and condition is not met
                if (!mFilter.IsEmpty)
                {
                    if (mFilter.Execute() == 0.0)
                    {
                        continue;
                    }
                }

                SaplingCell[]? saplingCells = ru.SaplingCells;
                if (saplingCells != null)
                {
                    for (int lightCellIndex = 0; lightCellIndex < saplingCells.Length; ++lightCellIndex)
                    {
                        SaplingCell saplingCell = saplingCells[lightCellIndex];
                        int n_on_px = saplingCell.GetOccupiedSlotCount();
                        if (n_on_px > 0)
                        {
                            for (int index = 0; index < saplingCell.Saplings.Length; ++index)
                            {
                                if (saplingCell.Saplings[index].IsOccupied())
                                {
                                    ResourceUnitTreeSpecies ruSpecies = saplingCell.Saplings[index].GetResourceUnitSpecies(ru);
                                    TreeSpecies treeSpecies = ruSpecies.Species;
                                    float dbh = 100.0F * saplingCell.Saplings[index].Height / treeSpecies.SaplingGrowthParameters.HeightDiameterRatio;
                                    // check minimum dbh
                                    if (dbh < this.mMinDbh)
                                    {
                                        continue;
                                    }
                                    float n_repr = treeSpecies.SaplingGrowthParameters.RepresentedStemNumberFromHeight(saplingCell.Saplings[index].Height) / n_on_px;

                                    insertRow.Parameters[0].Value = model.CurrentYear;
                                    insertRow.Parameters[1].Value = ru.ResourceUnitGridIndex;
                                    insertRow.Parameters[2].Value = ru.EnvironmentID;
                                    insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                                    insertRow.Parameters[4].Value = n_repr;
                                    insertRow.Parameters[5].Value = dbh;
                                    insertRow.Parameters[6].Value = saplingCell.Saplings[index].Height;
                                    insertRow.Parameters[7].Value = saplingCell.Saplings[index].Age;
                                    insertRow.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void Setup(Model model)
        {
            this.mFilter.SetExpression(model.Project.Output.SaplingDetail.Condition);
            mMinDbh = model.Project.Output.SaplingDetail.MinDbh;
        }
    }
}
