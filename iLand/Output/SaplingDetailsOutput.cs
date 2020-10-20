﻿using iLand.Simulation;
using iLand.Tools;
using iLand.Trees;
using iLand.World;
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
                    SaplingCell saplingCell = saplingCells[px];
                    int n_on_px = saplingCell.GetOccupiedSlotCount();
                    if (n_on_px > 0)
                    {
                        for (int index = 0; index < SaplingCell.SaplingsPerCell; ++index)
                        {
                            if (saplingCell.Saplings[index].IsOccupied())
                            {
                                ResourceUnitSpecies rus = saplingCell.Saplings[index].ResourceUnitSpecies(ru);
                                Species species = rus.Species;
                                double dbh = saplingCell.Saplings[index].Height / species.SaplingGrowthParameters.HdSapling * 100.0;
                                // check minimum dbh
                                if (dbh < mMinDbh)
                                {
                                    continue;
                                }
                                double n_repr = species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(saplingCell.Saplings[index].Height) / n_on_px;

                                insertRow.Parameters[0].Value = model.ModelSettings.CurrentYear;
                                insertRow.Parameters[1].Value = ru.Index;
                                insertRow.Parameters[2].Value = ru.ID;
                                insertRow.Parameters[3].Value = rus.Species.ID;
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

        public override void Setup(Model model)
        {
            mFilter.SetExpression(model.Project.Output.SaplingDetail.Condition);
            mMinDbh = model.Project.Output.SaplingDetail.MinDbh;
        }
    }
}
