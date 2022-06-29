using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;

namespace iLand.Output
{
    public class TreeRemovedAnnualOutput : AnnualOutput
    {
        private readonly Expression treeFilter;
        private readonly Dictionary<ResourceUnit, (Trees Trees, List<MortalityCause> Removals)> removedTreesByResourceUnit;

        public TreeRemovedAnnualOutput()
        {
            this.treeFilter = new Expression();
            this.removedTreesByResourceUnit = new Dictionary<ResourceUnit, (Trees, List<MortalityCause>)>();

            this.Name = "Tree Removed Output";
            this.TableName = "treeRemoved";
            this.Description = "Output of removed indivdual trees. Use the ''filter'' property to reduce amount of data (filter by resource-unit, year, species, ...)." + System.Environment.NewLine +
                               "The output is triggered immediately when a tree is removed due to mortality or management.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateResourceUnit());
            this.Columns.Add(SqlColumn.CreateID());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new SqlColumn("id", "id of the tree", SqliteType.Integer));
            this.Columns.Add(new SqlColumn("reason", "reason of removal: 0: mortality, 1: management, 2: disturbance ", SqliteType.Integer));
            this.Columns.Add(new SqlColumn("x", "position of the tree, x-direction (m)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("y", "position of the tree, y-direction (m)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("dbh", "dbh (cm) of the tree", SqliteType.Real));
            this.Columns.Add(new SqlColumn("height", "height (m) of the tree", SqliteType.Real));
            this.Columns.Add(new SqlColumn("basalArea", "basal area of tree in m2", SqliteType.Real));
            this.Columns.Add(new SqlColumn("volume_m3", "volume of tree (m3)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("leafArea_m2", "current leaf area of the tree (m2)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("foliageMass", "current mass of foliage (kg)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("woodyMass", "kg Biomass in woody department", SqliteType.Real));
            this.Columns.Add(new SqlColumn("fineRootMass", "kg Biomass in fine-root department", SqliteType.Real));
            this.Columns.Add(new SqlColumn("coarseRootMass", "kg Biomass in coarse-root department", SqliteType.Real));
            this.Columns.Add(new SqlColumn("lri", "LightResourceIndex of the tree (raw light index from iLand, without applying resource-unit modifications)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("lightResponse", "light response value (including species specific response to the light level)", SqliteType.Real));
            this.Columns.Add(new SqlColumn("stressIndex", "scalar (0..1) indicating the stress level (see [Mortality]).", SqliteType.Real));
            this.Columns.Add(new SqlColumn("reserve_kg", "NPP currently available in the reserve pool (kg Biomass)", SqliteType.Real));
        }

        public bool TryAddTree(Model model, Trees trees, int treeIndex, MortalityCause reason)
        {
            if (this.treeFilter.IsEmpty == false)
            {
                // skip trees if filter is present
                TreeWrapper treeWrapper = new(model)
                {
                    Trees = trees,
                    TreeIndex = treeIndex
                };
                this.treeFilter.Wrapper = treeWrapper;
                if (this.treeFilter.Execute() == 0.0)
                {
                    return false;
                }
            }

            if (this.removedTreesByResourceUnit.TryGetValue(trees.RU, out (Trees Trees, List<MortalityCause> Removals) removedTreesOfSpecies) == false)
            {
                removedTreesOfSpecies = new(new Trees(model.Landscape, trees.RU, trees.Species), new List<MortalityCause>());
                this.removedTreesByResourceUnit.Add(trees.RU, removedTreesOfSpecies);
            }

            removedTreesOfSpecies.Trees.Add(trees, treeIndex);
            removedTreesOfSpecies.Removals.Add(reason);
            return true;
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach ((Trees Trees, List<MortalityCause> Removals) removedTreesOfSpecies in this.removedTreesByResourceUnit.Values)
            {
                Trees trees = removedTreesOfSpecies.Trees;
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    insertRow.Parameters[0].Value = model.CurrentYear;
                    insertRow.Parameters[1].Value = trees.RU.ResourceUnitGridIndex;
                    insertRow.Parameters[2].Value = trees.RU.EnvironmentID;
                    insertRow.Parameters[3].Value = trees.Species.ID;
                    insertRow.Parameters[4].Value = trees.Tag[treeIndex];
                    insertRow.Parameters[5].Value = (int)removedTreesOfSpecies.Removals[treeIndex];
                    insertRow.Parameters[6].Value = trees.GetCellCenterPoint(treeIndex).X;
                    insertRow.Parameters[7].Value = trees.GetCellCenterPoint(treeIndex).Y;
                    insertRow.Parameters[8].Value = trees.Dbh[treeIndex];
                    insertRow.Parameters[9].Value = trees.Height[treeIndex];
                    insertRow.Parameters[10].Value = trees.GetBasalArea(treeIndex);
                    insertRow.Parameters[11].Value = trees.GetStemVolume(treeIndex);
                    insertRow.Parameters[12].Value = trees.LeafArea[treeIndex];
                    insertRow.Parameters[13].Value = trees.FoliageMass[treeIndex];
                    insertRow.Parameters[14].Value = trees.StemMass[treeIndex];
                    insertRow.Parameters[15].Value = trees.FineRootMass[treeIndex];
                    insertRow.Parameters[16].Value = trees.CoarseRootMass[treeIndex];
                    insertRow.Parameters[17].Value = trees.LightResourceIndex[treeIndex];
                    insertRow.Parameters[18].Value = trees.LightResponse[treeIndex];
                    insertRow.Parameters[19].Value = trees.StressIndex[treeIndex];
                    insertRow.Parameters[20].Value = trees.NppReserve[treeIndex];
                    insertRow.ExecuteNonQuery();
                }
            }

            this.removedTreesByResourceUnit.Clear();
        }

        public override void Setup(Model model)
        {
            this.treeFilter.SetExpression(model.Project.Output.Annual.TreeRemoved.Filter);
        }
    }
}
