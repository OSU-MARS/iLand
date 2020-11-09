using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class TreeOutput : Output
    {
        private readonly Expression mFilter;

        public TreeOutput()
        {
            this.mFilter = new Expression();

            this.Name = "Tree Output";
            this.TableName = "tree";
            this.Description = "Output of indivdual trees. Use the 'filter' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                               "The output is triggered after the growth of the current season. " +
                               "Initial values (without any growth) are output as 'startyear-1'.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new SqlColumn("id", "id of the tree", OutputDatatype.Integer));
            this.Columns.Add(new SqlColumn("x", "position of the tree, x-direction (m)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("y", "position of the tree, y-direction (m)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("dbh", "dbh (cm) of the tree", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("height", "height (m) of the tree", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("basalArea", "basal area of tree in m2", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("volume_m3", "volume of tree (m3)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("leafArea_m2", "current leaf area of the tree (m2)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("foliageMass", "current mass of foliage (kg)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("woodyMass", "kg Biomass in woody department", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("fineRootMass", "kg Biomass in fine-root department", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("coarseRootMass", "kg Biomass in coarse-root department", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("lri", "LightResourceIndex of the tree (raw light index from iLand, without applying resource-unit modifications)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("lightResponse", "light response value (including species specific response to the light level)", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("stressIndex", "scalar (0..1) indicating the stress level (see [Mortality]).", OutputDatatype.Double));
            this.Columns.Add(new SqlColumn("reserve_kg", "NPP currently available in the reserve pool (kg Biomass)", OutputDatatype.Double));
        }

        public override void Setup(Model model)
        {
            mFilter.SetExpression(model.Project.Output.Tree.Filter);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            AllTreesEnumerator allTreeEnumerator = new AllTreesEnumerator(model.Landscape);
            //using DebugTimer dt = model.DebugTimers.Create("TreeOutput.LogYear()");
            TreeWrapper treeWrapper = new TreeWrapper(model);
            mFilter.Wrapper = treeWrapper;
            while (allTreeEnumerator.MoveNext())
            {
                Trees treesOfSpecies = allTreeEnumerator.CurrentTrees;
                int treeIndex = allTreeEnumerator.CurrentTreeIndex;
                if (mFilter.IsEmpty == false)
                { 
                    // nothing to log if tree is excluded by filter
                    treeWrapper.Trees = treesOfSpecies;
                    treeWrapper.TreeIndex = treeIndex;
                    if (mFilter.Execute() == 0.0)
                    {
                        continue;
                    }
                }

                insertRow.Parameters[0].Value = model.CurrentYear;
                insertRow.Parameters[1].Value = treesOfSpecies.RU.ResourceUnitGridIndex;
                insertRow.Parameters[2].Value = treesOfSpecies.RU.EnvironmentID;
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
