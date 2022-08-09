using iLand.Input.ProjectFile;
using iLand.Simulation;
using iLand.Tree;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Model = iLand.Simulation.Model;

namespace iLand.Output.Sql
{
    /** LandscapeRemovedOut is aggregated output for removed trees on the full landscape. All values are per hectare values. */
    public class LandscapeRemovedAnnualOutput : AnnualOutput
    {
        private const int KeyRemovalTypeMultiplier = 10000;

        private bool includeDeadTrees;
        private bool includeHarvestTrees;
        private readonly Dictionary<int, LandscapeRemovalData> removalsByTypeAndSpeciesIndex;

        public LandscapeRemovedAnnualOutput()
        {
            this.removalsByTypeAndSpeciesIndex = new();

            this.includeDeadTrees = false;
            this.includeHarvestTrees = true;

            this.Name = "Aggregates of removed trees due to death, harvest, and disturbances per species";
            this.TableName = "landscape_removed";
            this.Description = "Aggregates of all removed trees due to 'natural' death, harvest, or disturbance per species and reason. All values are totals for the whole landscape." +
                               "The user can select with options whether to include 'natural' death and harvested trees (which may slow down the processing). " +
                               "Set the setting in the XML project file 'includeNatural' to 'true' to include trees that died due to natural mortality, " +
                               "the setting 'includeHarvest' controls whether to include ('true') or exclude ('false') harvested trees.";
            this.Columns.Add(SqlColumn.CreateYear());
            this.Columns.Add(SqlColumn.CreateSpecies());
            this.Columns.Add(new SqlColumn("reason", "Resaon for tree death: 'N': Natural mortality, 'H': Harvest (removed from the forest), 'D': Disturbance (not salvage-harvested), 'S': Salvage harvesting (i.e. disturbed trees which are harvested), 'C': killed/cut down by management", SqliteType.Text));
            this.Columns.Add(new SqlColumn("count", "number of died trees (living, >4m height) ", SqliteType.Integer));
            this.Columns.Add(new SqlColumn("volume_m3", "sum of volume (geomery, taper factor) in m3", SqliteType.Real));
            this.Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", SqliteType.Real));
        }

        public void AddTree(Tree.Trees trees, int treeIndex, MortalityCause removalType)
        {
            if (this.includeDeadTrees == false && (removalType == MortalityCause.Stress))
            {
                return;
            }
            if (this.includeHarvestTrees == false && (removalType == MortalityCause.Harvest || removalType == MortalityCause.Salavaged || removalType == MortalityCause.CutDown))
            {
                return;
            }

            Debug.Assert(trees.Species.Index < LandscapeRemovedAnnualOutput.KeyRemovalTypeMultiplier);
            int key = LandscapeRemovedAnnualOutput.KeyRemovalTypeMultiplier * (int)removalType + trees.Species.Index;
            if (this.removalsByTypeAndSpeciesIndex.TryGetValue(key, out LandscapeRemovalData? removalData) == false)
            {
                removalData = new(trees.Species);
                this.removalsByTypeAndSpeciesIndex.Add(key, removalData);
            }
            removalData.BasalArea += trees.GetBasalArea(treeIndex);
            removalData.Volume += trees.GetStemVolume(treeIndex);
            ++removalData.Count;
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach ((int removalKey, LandscapeRemovalData removalData) in this.removalsByTypeAndSpeciesIndex)
            {
                if (removalData.Count > 0)
                {
                    MortalityCause removalType = (MortalityCause)(removalKey / LandscapeRemovedAnnualOutput.KeyRemovalTypeMultiplier);
                    int speciesIndex = removalKey % LandscapeRemovedAnnualOutput.KeyRemovalTypeMultiplier;
                    insertRow.Parameters[0].Value = model.SimulationState.CurrentCalendarYear;
                    insertRow.Parameters[1].Value = removalData.TreeSpecies.ID;
                    insertRow.Parameters[2].Value = removalType switch
                    {
                        MortalityCause.CutDown => "C",
                        MortalityCause.Stress => "N",
                        MortalityCause.Disturbance => "D",
                        MortalityCause.Harvest => "H",
                        MortalityCause.Salavaged => "S",
                        _ => throw new NotSupportedException("Unhandled tree removal type " + removalType + ".")
                    };
                    insertRow.Parameters[3].Value = removalData.Count;
                    insertRow.Parameters[4].Value = removalData.Volume;
                    insertRow.Parameters[5].Value = removalData.BasalArea;
                    insertRow.ExecuteNonQuery();
                }
            }

            // clear data (no need to clear the hash table, right?)
            foreach (LandscapeRemovalData removal in this.removalsByTypeAndSpeciesIndex.Values)
            {
                removal.Zero();
            }
        }

        public override void Setup(Project projectFile, SimulationState simulationState)
        {
            this.includeHarvestTrees = projectFile.Output.Sql.LandscapeRemoved.IncludeHarvest;
            this.includeDeadTrees = projectFile.Output.Sql.LandscapeRemoved.IncludeNatural;
        }

        private class LandscapeRemovalData
        {
            public float BasalArea { get; set; }
            public float Count { get; set; }
            public TreeSpecies TreeSpecies { get; private init; }
            public float Volume { get; set; }

            public LandscapeRemovalData(TreeSpecies treeSpecies)
            {
                this.TreeSpecies = treeSpecies;
                this.Zero();
            }

            public void Zero()
            {
                this.BasalArea = 0.0F;
                this.Count = 0.0F;
                this.Volume = 0.0F;
            }
        }
    }
}
