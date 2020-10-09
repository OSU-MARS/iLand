using iLand.Core;
using iLand.Tools;
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

        private readonly Dictionary<int, LROdata> mLandscapeRemoval;

        public LandscapeRemovedOutput()
        {
            this.mLandscapeRemoval = new Dictionary<int, LROdata>();

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

        public void AccumulateTreeRemoval(Tree tree, int reason)
        {
            MortalityCause rem_type = (MortalityCause)reason;
            if (rem_type == MortalityCause.Stress && !mIncludeDeadTrees)
            {
                return;
            }
            if ((rem_type == MortalityCause.Harvest || rem_type == MortalityCause.Salavaged || rem_type == MortalityCause.CutDown) && !mIncludeHarvestTrees)
            {
                return;
            }

            int key = reason * 10000 + tree.Species.Index;
            if (mLandscapeRemoval.TryGetValue(key, out LROdata removalData) == false)
            {
                removalData = new LROdata();
                mLandscapeRemoval.Add(key, removalData);
            }
            removalData.basal_area += tree.BasalArea();
            removalData.volume += tree.Volume();
            removalData.n++;
        }

        protected override void LogYear(Model model, SqliteCommand insertRow)
        {
            foreach (KeyValuePair<int, LROdata> i in mLandscapeRemoval)
            {
                if (i.Value.n > 0)
                {
                    MortalityCause rem_type = (MortalityCause)(i.Key / 10000);
                    int species_index = i.Key % 10000;
                    this.Add(model.GlobalSettings.CurrentYear);
                    this.Add(model.SpeciesSet().Species(species_index).ID);
                    this.Add(rem_type switch
                    {
                        MortalityCause.CutDown => "C",
                        MortalityCause.Stress => "N",
                        MortalityCause.Disturbance => "D",
                        MortalityCause.Harvest => "H",
                        MortalityCause.Salavaged => "S",
                        _ => throw new NotSupportedException("Unhandled tree removal type " + rem_type + ".")
                    });;
                    this.Add(i.Value.n);
                    this.Add(i.Value.volume);
                    this.Add(i.Value.basal_area);
                    this.WriteRow(insertRow);
                }
            }

            // clear data (no need to clear the hash table, right?)
            foreach (LROdata i in mLandscapeRemoval.Values)
            {
                i.Clear();
            }
        }

        public override void Setup(GlobalSettings globalSettings)
        {
            mIncludeHarvestTrees = globalSettings.Settings.GetBool(".includeHarvest", true);
            mIncludeDeadTrees = globalSettings.Settings.GetBool(".includeNatural", false);
            Tree.LandscapeRemovalOutput = this;
        }
    }
}
