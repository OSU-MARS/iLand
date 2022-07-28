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
    public class SaplingDetailsAnnualOutput : AnnualOutput
    {
        private readonly Expression resourceUnitFilter;
        private float minimumDbh;

        public SaplingDetailsAnnualOutput()
        {
            this.resourceUnitFilter = new();

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
            this.Columns.Add(new("n_represented", "number of trees that are represented by the cohort (Reineke function).", SqliteType.Real));
            this.Columns.Add(new("dbh", "diameter of the cohort (cm).", SqliteType.Real));
            this.Columns.Add(new("height", "height of the cohort (m).", SqliteType.Real));
            this.Columns.Add(new("age", "age of the cohort (years) ", SqliteType.Integer));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (ResourceUnit resourceUnit in model.Landscape.ResourceUnits)
            {
                // exclude if a condition is specified and condition is not met
                if (this.resourceUnitFilter.IsEmpty == false)
                {
                    Debug.Assert(this.resourceUnitFilter.Wrapper != null);
                    ((ResourceUnitVariableAccessor)this.resourceUnitFilter.Wrapper).ResourceUnit = resourceUnit;
                    if (this.resourceUnitFilter.Execute() == 0.0F)
                    {
                        continue;
                    }
                }

                SaplingCell[]? saplingCells = resourceUnit.SaplingCells;
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
                                    ResourceUnitTreeSpecies ruSpecies = saplingCell.Saplings[index].GetResourceUnitSpecies(resourceUnit);
                                    TreeSpecies treeSpecies = ruSpecies.Species;
                                    float dbh = 100.0F * saplingCell.Saplings[index].HeightInM / treeSpecies.SaplingGrowth.HeightDiameterRatio;
                                    // check minimum dbh
                                    if (dbh < this.minimumDbh)
                                    {
                                        continue;
                                    }
                                    float n_repr = treeSpecies.SaplingGrowth.RepresentedStemNumberFromHeight(saplingCell.Saplings[index].HeightInM) / n_on_px;

                                    insertRow.Parameters[0].Value = model.SimulationState.CurrentYear;
                                    insertRow.Parameters[1].Value = resourceUnit.ResourceUnitGridIndex;
                                    insertRow.Parameters[2].Value = resourceUnit.ID;
                                    insertRow.Parameters[3].Value = ruSpecies.Species.ID;
                                    insertRow.Parameters[4].Value = n_repr;
                                    insertRow.Parameters[5].Value = dbh;
                                    insertRow.Parameters[6].Value = saplingCell.Saplings[index].HeightInM;
                                    insertRow.Parameters[7].Value = saplingCell.Saplings[index].Age;
                                    insertRow.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            this.resourceUnitFilter.SetExpression(projectFile.Output.Sql.SaplingDetail.Condition);
            this.resourceUnitFilter.Wrapper = new ResourceUnitVariableAccessor(simulationState);
            this.minimumDbh = projectFile.Output.Sql.SaplingDetail.MinDbh;
        }
    }
}
