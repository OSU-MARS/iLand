using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using Microsoft.Data.Sqlite;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    public class TreesAnnualOutput : AnnualOutput
    {
        private readonly Expression treeFilter;

        public TreesAnnualOutput()
        {
            this.treeFilter = new();

            this.Name = "Tree Output";
            this.TableName = "tree";
            this.Description = "Output of indivdual trees. Use the 'filter' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                               "The output is triggered after the growth of the current season. " +
                               "Initial values (without any growth) are output as 'startyear-1'.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new("id", "id of the tree", SqliteType.Integer));
            this.Columns.Add(new("x", "position of the tree, x-direction (m)", SqliteType.Real));
            this.Columns.Add(new("y", "position of the tree, y-direction (m)", SqliteType.Real));
            this.Columns.Add(new("dbh", "dbh (cm) of the tree", SqliteType.Real));
            this.Columns.Add(new("height", "height (m) of the tree", SqliteType.Real));
            this.Columns.Add(new("basalArea", "basal area of tree in m2", SqliteType.Real));
            this.Columns.Add(new("volume_m3", "volume of tree (m3)", SqliteType.Real));
            this.Columns.Add(new("leafArea_m2", "current leaf area of the tree (m2)", SqliteType.Real));
            this.Columns.Add(new("foliageMass", "current mass of foliage (kg)", SqliteType.Real));
            this.Columns.Add(new("woodyMass", "kg Biomass in woody department", SqliteType.Real));
            this.Columns.Add(new("fineRootMass", "kg Biomass in fine-root department", SqliteType.Real));
            this.Columns.Add(new("coarseRootMass", "kg Biomass in coarse-root department", SqliteType.Real));
            this.Columns.Add(new("lri", "LightResourceIndex of the tree (raw light index from iLand, without applying resource-unit modifications)", SqliteType.Real));
            this.Columns.Add(new("lightResponse", "light response value (including species specific response to the light level)", SqliteType.Real));
            this.Columns.Add(new("stressIndex", "scalar (0..1) indicating the stress level (see [Mortality]).", SqliteType.Real));
            this.Columns.Add(new("reserve_kg", "NPP currently available in the reserve pool (kg Biomass)", SqliteType.Real));
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            this.treeFilter.SetExpression(projectFile.Output.Sql.Tree.Filter);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            AllTreesEnumerator allTreeEnumerator = new(model.Landscape);
            TreeVariableAccessor treeWrapper = new(model.SimulationState);
            this.treeFilter.Wrapper = treeWrapper;
            while (allTreeEnumerator.MoveNext())
            {
                Trees treesOfSpecies = allTreeEnumerator.CurrentTrees;
                int treeIndex = allTreeEnumerator.CurrentTreeIndex;
                if (this.treeFilter.IsEmpty == false)
                { 
                    // nothing to log if tree is excluded by filter
                    treeWrapper.Trees = treesOfSpecies;
                    treeWrapper.TreeIndex = treeIndex;
                    if (this.treeFilter.Execute() == 0.0F)
                    {
                        continue;
                    }
                }

                insertRow.Parameters[0].Value = model.SimulationState.CurrentYear;
                insertRow.Parameters[1].Value = treesOfSpecies.ResourceUnit.ResourceUnitGridIndex;
                insertRow.Parameters[2].Value = treesOfSpecies.ResourceUnit.ID;
                insertRow.Parameters[3].Value = treesOfSpecies.Species.ID;
                insertRow.Parameters[4].Value = treesOfSpecies.Tag[treeIndex];
                insertRow.Parameters[5].Value = treesOfSpecies.GetCellCenterPoint(treeIndex).X;
                insertRow.Parameters[6].Value = treesOfSpecies.GetCellCenterPoint(treeIndex).Y;
                insertRow.Parameters[7].Value = treesOfSpecies.Dbh[treeIndex];
                insertRow.Parameters[8].Value = treesOfSpecies.Height[treeIndex];
                insertRow.Parameters[9].Value = treesOfSpecies.GetBasalArea(treeIndex);
                insertRow.Parameters[10].Value = treesOfSpecies.GetStemVolume(treeIndex);
                insertRow.Parameters[11].Value = treesOfSpecies.LeafArea[treeIndex];
                insertRow.Parameters[12].Value = treesOfSpecies.FoliageMass[treeIndex];
                insertRow.Parameters[13].Value = treesOfSpecies.StemMass[treeIndex];
                insertRow.Parameters[14].Value = treesOfSpecies.FineRootMass[treeIndex];
                insertRow.Parameters[15].Value = treesOfSpecies.CoarseRootMass[treeIndex];
                insertRow.Parameters[16].Value = treesOfSpecies.LightResourceIndex[treeIndex];
                insertRow.Parameters[17].Value = treesOfSpecies.LightResponse[treeIndex];
                insertRow.Parameters[18].Value = treesOfSpecies.StressIndex[treeIndex];
                insertRow.Parameters[19].Value = treesOfSpecies.NppReserve[treeIndex];
                insertRow.ExecuteNonQuery();
            }
        }
    }
}
