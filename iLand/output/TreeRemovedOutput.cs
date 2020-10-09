using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;

namespace iLand.Output
{
    public class TreeRemovedOutput : Output
    {
        private readonly Expression mFilter;

        public TreeRemovedOutput()
        {
            this.mFilter = new Expression();

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

        public void LogTreeRemoval(GlobalSettings globalSettings, Tree tree, int reason)
        {
            if (!mFilter.IsEmpty)
            { 
                // skip trees if filter is present
                TreeWrapper tw = new TreeWrapper();
                mFilter.Wrapper = tw;
                tw.Tree = tree;
                if (mFilter.Execute(globalSettings) == 0.0)
                {
                    return;
                }
            }

            // TODO: re-enable tree removal logging
            //this.Add(globalSettings.CurrentYear);
            //this.Add(tree.RU.Index);
            //this.Add(tree.RU.ID);
            //this.Add(tree.Species.ID);
            //this.Add(tree.ID);
            //this.Add(reason);
            //this.Add(tree.GetCellCenterPoint().X);
            //this.Add(tree.GetCellCenterPoint().Y);
            //this.Add(tree.Dbh);
            //this.Add(tree.Height);
            //this.Add(tree.BasalArea());
            //this.Add(tree.Volume());
            //this.Add(tree.LeafArea);
            //this.Add(tree.FoliageMass);
            //this.Add(tree.StemMass);
            //this.Add(tree.FineRootMass);
            //this.Add(tree.CoarseRootMass);
            //this.Add(tree.LightResourceIndex);
            //this.Add(tree.mLightResponse);
            //this.Add(tree.StressIndex);
            //this.Add(tree.mNPPReserve);
            //this.WriteRow(insertRow: null);
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            // do nothing here
            return;
        }

        public override void Setup(GlobalSettings globalSettings)
        {
            string filter = globalSettings.Settings.GetString(".filter", "");
            mFilter.SetExpression(filter);
            Tree.TreeRemovalOutput = this;
        }
    }
}
