using iLand.Simulation;
using iLand.Tools;
using iLand.Trees;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace iLand.Output
{
    public class TreeRemovedOutput : Output
    {
        private readonly Expression filter;
        private readonly List<MortalityCause> removalReasons;
        private readonly List<Tree> removedTrees;

        public TreeRemovedOutput()
        {
            this.filter = new Expression();
            this.removalReasons = new List<MortalityCause>();
            this.removedTrees = new List<Tree>();

            Name = "Tree Removed Output";
            TableName = "treeremoved";
            Description = "Output of removed indivdual trees. Use the ''filter'' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                          "The output is triggered immediately when a tree is removed due to mortality or management.";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateResourceUnit());
            Columns.Add(SqlColumn.CreateID());
            Columns.Add(SqlColumn.CreateSpecies());
            Columns.Add(new SqlColumn("id", "id of the tree", OutputDatatype.Integer));
            Columns.Add(new SqlColumn("reason", "reason of removal: 0: mortality, 1: management, 2: disturbance ", OutputDatatype.Integer));
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

        public void AddTree(Model model, Tree tree, MortalityCause reason)
        {
            if (filter.IsEmpty == false)
            { 
                // skip trees if filter is present
                TreeWrapper tw = new TreeWrapper();
                filter.Wrapper = tw;
                tw.Tree = tree;
                if (filter.Execute(model) == 0.0)
                {
                    return;
                }
            }

            this.removedTrees.Add(tree);
            this.removalReasons.Add(reason);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            for (int treeIndex = 0; treeIndex < this.removedTrees.Count; ++treeIndex)
            {
                Tree tree = this.removedTrees[treeIndex];
                insertRow.Parameters[0].Value = model.GlobalSettings.CurrentYear;
                insertRow.Parameters[1].Value = tree.RU.Index;
                insertRow.Parameters[2].Value = tree.RU.ID;
                insertRow.Parameters[3].Value = tree.Species.ID;
                insertRow.Parameters[4].Value = tree.ID;
                insertRow.Parameters[5].Value = (int)this.removalReasons[treeIndex];
                insertRow.Parameters[6].Value = tree.GetCellCenterPoint().X;
                insertRow.Parameters[7].Value = tree.GetCellCenterPoint().Y;
                insertRow.Parameters[8].Value = tree.Dbh;
                insertRow.Parameters[9].Value = tree.Height;
                insertRow.Parameters[10].Value = tree.BasalArea();
                insertRow.Parameters[11].Value = tree.Volume();
                insertRow.Parameters[12].Value = tree.LeafArea;
                insertRow.Parameters[13].Value = tree.FoliageMass;
                insertRow.Parameters[14].Value = tree.StemMass;
                insertRow.Parameters[15].Value = tree.FineRootMass;
                insertRow.Parameters[16].Value = tree.CoarseRootMass;
                insertRow.Parameters[17].Value = tree.LightResourceIndex;
                insertRow.Parameters[18].Value = tree.LightResponse;
                insertRow.Parameters[19].Value = tree.StressIndex;
                insertRow.Parameters[20].Value = tree.NppReserve;
                insertRow.ExecuteNonQuery();
            }

            this.removedTrees.Clear();
            this.removalReasons.Clear();
        }

        public override void Setup(GlobalSettings globalSettings)
        {
            string filter = globalSettings.Settings.GetString(".filter", "");
            this.filter.SetExpression(filter);
        }
    }
}
