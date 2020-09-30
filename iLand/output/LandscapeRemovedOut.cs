using iLand.core;
using iLand.tools;
using System.Collections.Generic;

namespace iLand.output
{
    /** LandscapeRemovedOut is aggregated output for removed trees on the full landscape. All values are per hectare values. */
    internal class LandscapeRemovedOut : Output
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

        public LandscapeRemovedOut()
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
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateSpecies());
            Columns.Add(new OutputColumn("reason", "Resaon for tree death: 'N': Natural mortality, 'H': Harvest (removed from the forest), 'D': Disturbance (not salvage-harvested), 'S': Salvage harvesting (i.e. disturbed trees which are harvested), 'C': killed/cut down by management", OutputDatatype.OutString));
            Columns.Add(new OutputColumn("count", "number of died trees (living, >4m height) ", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("volume_m3", "sum of volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.OutDouble));
        }

        public void ExecRemovedTree(Tree t, int reason)
        {
            TreeRemovalType rem_type = (TreeRemovalType)reason;
            if (rem_type == TreeRemovalType.TreeDeath && !mIncludeDeadTrees)
            {
                return;
            }
            if ((rem_type == TreeRemovalType.TreeHarvest || rem_type == TreeRemovalType.TreeSalavaged || rem_type == TreeRemovalType.TreeCutDown) && !mIncludeHarvestTrees)
            {
                return;
            }

            int key = reason * 10000 + t.Species.Index;
            LROdata d = mLandscapeRemoval[key];
            d.basal_area += t.BasalArea();
            d.volume += t.Volume();
            d.n++;
        }

        public override void Exec()
        {
            foreach (KeyValuePair<int, LROdata> i in mLandscapeRemoval)
            {
                if (i.Value.n > 0)
                {
                    TreeRemovalType rem_type = (TreeRemovalType)(i.Key / 10000);
                    int species_index = i.Key % 10000;
                    this.Add(CurrentYear());
                    this.Add(GlobalSettings.Instance.Model.SpeciesSet().Species(species_index).ID);
                    if (rem_type == TreeRemovalType.TreeDeath) this.Add("N");
                    if (rem_type == TreeRemovalType.TreeHarvest) this.Add("H");
                    if (rem_type == TreeRemovalType.TreeDisturbance) this.Add("D");
                    if (rem_type == TreeRemovalType.TreeSalavaged) this.Add("S");
                    if (rem_type == TreeRemovalType.TreeCutDown) this.Add("C");
                    this.Add(i.Value.n);
                    this.Add(i.Value.volume);
                    this.Add(i.Value.basal_area);
                    WriteRow();
                }
            }

            // clear data (no need to clear the hash table, right?)
            foreach (LROdata i in mLandscapeRemoval.Values)
            {
                i.Clear();
            }
        }

        public override void Setup()
        {
            mIncludeHarvestTrees = Settings().ValueBool(".includeHarvest", true);
            mIncludeDeadTrees = Settings().ValueBool(".includeNatural", false);
            Tree.LandscapeRemovalOutput = this;
        }
    }
}
