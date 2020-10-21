using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;

namespace iLand.Output
{
    /** LandscapeRemovedOut is aggregated output for removed trees on the full landscape. All values are per hectare values. */
    public class LandscapeRemovedOutput : Output
    {
        private bool mIncludeDeadTrees;
        private bool mIncludeHarvestTrees;

        private class LROdata
        {
            public double volume;
            public double basal_area;
            public double n;

            public LROdata()
            {
                Clear();
            }

            public void Clear()
            {
                volume = 0.0;
                basal_area = 0.0; 
                n = 0.0;
            }
        }

        private readonly Dictionary<int, LROdata> removals;

        public LandscapeRemovedOutput()
        {
            this.removals = new Dictionary<int, LROdata>();

            mIncludeDeadTrees = false;
            mIncludeHarvestTrees = true;

            Name = "Aggregates of removed trees due to death, harvest, and disturbances per species";
            TableName = "landscape_removed";
            Description = "Aggregates of all removed trees due to 'natural' death, harvest, or disturbance per species and reason. All values are totals for the whole landscape." +
                          "The user can select with options whether to include 'natural' death and harvested trees (which may slow down the processing). " +
                          "Set the setting in the XML project file 'includeNatural' to 'true' to include trees that died due to natural mortality, " +
                          "the setting 'includeHarvest' controls whether to include ('true') or exclude ('false') harvested trees.";
            Columns.Add(SqlColumn.CreateYear());
            Columns.Add(SqlColumn.CreateSpecies());
            Columns.Add(new SqlColumn("reason", "Resaon for tree death: 'N': Natural mortality, 'H': Harvest (removed from the forest), 'D': Disturbance (not salvage-harvested), 'S': Salvage harvesting (i.e. disturbed trees which are harvested), 'C': killed/cut down by management", OutputDatatype.String));
            Columns.Add(new SqlColumn("count", "number of died trees (living, >4m height) ", OutputDatatype.Integer));
            Columns.Add(new SqlColumn("volume_m3", "sum of volume (geomery, taper factor) in m3", OutputDatatype.Double));
            Columns.Add(new SqlColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.Double));
        }

        public void AddTree(Tree.Trees trees, int treeIndex, MortalityCause removalType)
        {
            if (removalType == MortalityCause.Stress && !mIncludeDeadTrees)
            {
                return;
            }
            if ((removalType == MortalityCause.Harvest || removalType == MortalityCause.Salavaged || removalType == MortalityCause.CutDown) && !mIncludeHarvestTrees)
            {
                return;
            }

            int key = 10000 * (int)removalType + trees.Species.Index;
            if (removals.TryGetValue(key, out LROdata removalData) == false)
            {
                removalData = new LROdata();
                removals.Add(key, removalData);
            }
            removalData.basal_area += trees.GetBasalArea(treeIndex);
            removalData.volume += trees.GetStemVolume(treeIndex);
            removalData.n++;
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (KeyValuePair<int, LROdata> removal in removals)
            {
                if (removal.Value.n > 0)
                {
                    MortalityCause rem_type = (MortalityCause)(removal.Key / 10000);
                    int species_index = removal.Key % 10000;
                    insertRow.Parameters[0].Value = model.ModelSettings.CurrentYear;
                    insertRow.Parameters[1].Value = model.GetFirstSpeciesSet().Species(species_index).ID;
                    insertRow.Parameters[2].Value = rem_type switch
                    {
                        MortalityCause.CutDown => "C",
                        MortalityCause.Stress => "N",
                        MortalityCause.Disturbance => "D",
                        MortalityCause.Harvest => "H",
                        MortalityCause.Salavaged => "S",
                        _ => throw new NotSupportedException("Unhandled tree removal type " + rem_type + ".")
                    };;
                    insertRow.Parameters[3].Value = removal.Value.n;
                    insertRow.Parameters[4].Value = removal.Value.volume;
                    insertRow.Parameters[5].Value = removal.Value.basal_area;
                    insertRow.ExecuteNonQuery();
                }
            }

            // clear data (no need to clear the hash table, right?)
            foreach (LROdata removal in this.removals.Values)
            {
                removal.Clear();
            }
        }

        public override void Setup(Model model)
        {
            mIncludeHarvestTrees = model.Project.Output.LandscapeRemoved.IncludeHarvest;
            mIncludeDeadTrees = model.Project.Output.LandscapeRemoved.IncludeNatural;
        }
    }
}
