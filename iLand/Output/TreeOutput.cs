using iLand.Simulation;
using iLand.Tools;
using iLand.Trees;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class TreeOutput : Output
    {
        private readonly Expression mFilter;

        public TreeOutput()
        {
            this.mFilter = new Expression();

            Name = "Tree Output";
            TableName = "tree";
            Description = "Output of indivdual trees. Use the ''filter'' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                          "The output is triggered after the growth of the current season. " +
                          "Initial values (without any growth) are output as 'startyear-1'.";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(SqlColumn.CreateSpecies());
            Columns.Add(new SqlColumn("id", "id of the tree", OutputDatatype.Integer));
            Columns.Add(new SqlColumn("x", "position of the tree, x-direction (m)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("y", "position of the tree, y-direction (m)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("dbh", "dbh (cm) of the tree", OutputDatatype.Double));
            Columns.Add(new SqlColumn("height", "height (m) of the tree", OutputDatatype.Double));
            Columns.Add(new SqlColumn("basalArea", "basal area of tree in m2", OutputDatatype.Double));
            Columns.Add(new SqlColumn("volume_m3", "volume of tree (m3)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("leafArea_m2", "current leaf area of the tree (m2)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("foliageMass", "current mass of foliage (kg)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("woodyMass", "kg Biomass in woody department", OutputDatatype.Double));
            Columns.Add(new SqlColumn("fineRootMass", "kg Biomass in fine-root department", OutputDatatype.Double));
            Columns.Add(new SqlColumn("coarseRootMass", "kg Biomass in coarse-root department", OutputDatatype.Double));
            Columns.Add(new SqlColumn("lri", "LightResourceIndex of the tree (raw light index from iLand, without applying resource-unit modifications)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("lightResponse", "light response value (including species specific response to the light level)", OutputDatatype.Double));
            Columns.Add(new SqlColumn("stressIndex", "scalar (0..1) indicating the stress level (see [Mortality]).", OutputDatatype.Double));
            Columns.Add(new SqlColumn("reserve_kg", "NPP currently available in the reserve pool (kg Biomass)", OutputDatatype.Double));
        }

        public override void Setup(Model model)
        {
            mFilter.SetExpression(model.Project.Output.Tree.Filter);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            AllTreeIterator at = new AllTreeIterator(model);
            //using DebugTimer dt = model.DebugTimers.Create("TreeOutput.LogYear()");
            TreeWrapper tw = new TreeWrapper();
            mFilter.Wrapper = tw;
            for (Tree tree = at.MoveNext(); tree != null; tree = at.MoveNext())
            {
                if (mFilter.IsEmpty == false)
                { // skip fields
                    tw.Tree = tree;
                    if (mFilter.Execute(model) == 0.0)
                    {
                        continue;
                    }
                }
                insertRow.Parameters[0].Value = model.GlobalSettings.CurrentYear;
                insertRow.Parameters[1].Value = tree.RU.Index;
                insertRow.Parameters[2].Value = tree.RU.ID;
                insertRow.Parameters[3].Value = tree.Species.ID;
                insertRow.Parameters[4].Value = tree.ID;
                insertRow.Parameters[5].Value = tree.GetCellCenterPoint().X;
                insertRow.Parameters[6].Value = tree.GetCellCenterPoint().Y;
                insertRow.Parameters[7].Value = tree.Dbh;
                insertRow.Parameters[8].Value = tree.Height;
                insertRow.Parameters[9].Value = tree.BasalArea();
                insertRow.Parameters[10].Value = tree.Volume();
                insertRow.Parameters[11].Value = tree.LeafArea;
                insertRow.Parameters[12].Value = tree.FoliageMass;
                insertRow.Parameters[13].Value = tree.StemMass;
                insertRow.Parameters[14].Value = tree.FineRootMass;
                insertRow.Parameters[15].Value = tree.CoarseRootMass;
                insertRow.Parameters[16].Value = tree.LightResourceIndex;
                insertRow.Parameters[17].Value = tree.LightResponse;
                insertRow.Parameters[18].Value = tree.StressIndex;
                insertRow.Parameters[19].Value = tree.NppReserve;
                insertRow.ExecuteNonQuery();
            }
        }
    }
}
