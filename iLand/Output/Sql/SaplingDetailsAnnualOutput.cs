// C++/output/{ saplingout.h, saplingout.cpp }
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
    public class SaplingDetailsAnnualOutput : AnnualOutput // C++ SaplingDetailsOut
    {
        private float minimumDbh;
        private readonly Expression<ResourceUnitVariableAccessor> resourceUnitFilter;
        private readonly Expression<SaplingVariableAccessor> saplingFilter; // C++ mFilter

        public SaplingDetailsAnnualOutput()
        {
            this.resourceUnitFilter = new();
            this.saplingFilter = new();

            this.Name = "Sapling Details Output";
            this.TableName = "saplingDetail";
            this.Description = "Detailed output on indidvidual sapling cohorts." + System.Environment.NewLine +
                               "For each occupied and living 2x2m pixel, a row is generated, unless" +
                               "the tree diameter is below the 'minDbh' threshold (cm). " + System.Environment.NewLine +
                               "You can specify a 'condition' to limit execution for specific time/ area with the variables 'ru' (resource unit id) and 'year' (the current year)." + 
                               " and you can use the `filter` property to filter using sapling variables (such as species or x/y)";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnitID());
            this.Columns.Add(SqlColumn.CreateTreeSpeciesID());
            this.Columns.Add(new("n_represented", "number of trees that are represented by the cohort (Reineke function).", SqliteType.Real));
            this.Columns.Add(new("dbh", "diameter of the cohort (cm).", SqliteType.Real));
            this.Columns.Add(new("height", "height of the cohort (m).", SqliteType.Real));
            this.Columns.Add(new("age", "age of the cohort (years) ", SqliteType.Integer));
        }

        protected override void LogYear(Model model, SqliteCommand insertRow) // C++ SaplingDetailsOut::exec()
        {
            Debug.Assert((this.saplingFilter.Wrapper != null) && (this.resourceUnitFilter.Wrapper != null), $"{nameof(SaplingDetailAnnualOutput)}.{nameof(LogYear)}() called before {nameof(Setup)}().");
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
                                Sapling sapling = saplingCell.Saplings[index];
                                if (sapling.IsOccupied())
                                {
                                    ResourceUnitTreeSpecies ruSpecies = saplingCell.Saplings[index].GetResourceUnitSpecies(resourceUnit);
                                    TreeSpecies treeSpecies = ruSpecies.Species;
                                    float dbh = 100.0F * saplingCell.Saplings[index].HeightInM / treeSpecies.SaplingGrowth.HeightDiameterRatio;
                                    // check minimum dbh
                                    if (dbh < this.minimumDbh)
                                    {
                                        continue;
                                    }

                                    if (this.saplingFilter.IsEmpty == false)
                                    {
                                        this.saplingFilter.Wrapper.SetSapling(sapling, resourceUnit);
                                        if (this.saplingFilter.Execute() == 0.0F)
                                        {
                                            continue;
                                        }
                                    }

                                    float n_repr = treeSpecies.SaplingGrowth.RepresentedStemNumberFromHeight(sapling.HeightInM) / n_on_px;

                                    insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                                    insertRow.Parameters[1].Value = resourceUnit.ID;
                                    insertRow.Parameters[2].Value = ruSpecies.Species.WorldFloraID;
                                    insertRow.Parameters[3].Value = n_repr;
                                    insertRow.Parameters[4].Value = dbh;
                                    insertRow.Parameters[5].Value = sapling.HeightInM;
                                    insertRow.Parameters[6].Value = sapling.Age;
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
            this.minimumDbh = projectFile.Output.Sql.SaplingDetail.MinDbh;
            this.resourceUnitFilter.SetExpression(projectFile.Output.Sql.SaplingDetail.Condition);
            this.resourceUnitFilter.Wrapper = new ResourceUnitVariableAccessor(simulationState);
            this.saplingFilter.SetExpression(projectFile.Output.Sql.SaplingDetail.Filter);
            this.saplingFilter.Wrapper = new SaplingVariableAccessor(simulationState);
        }
    }
}
