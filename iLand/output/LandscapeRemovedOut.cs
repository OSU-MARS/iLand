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
        private Expression mCondition;
        private class LROdata
        {
            public double volume;
            public double basal_area;
            public double n;

            public LROdata()
            {
                clear();
            }
            public void clear()
            {
                volume = 0.0; basal_area = 0.0; n = 0.0;
            }
        };

        private Dictionary<int, LROdata> mLandscapeRemoval;

        public LandscapeRemovedOut()
        {
            mIncludeDeadTrees = false;
            mIncludeHarvestTrees = true;

            setName("Aggregates of removed trees due to death, harvest, and disturbances per species", "landscape_removed");
            setDescription("Aggregates of all removed trees due to 'natural' death, harvest, or disturbance per species and reason. All values are totals for the whole landscape." +
                       "The user can select with options whether to include 'natural' death and harvested trees (which may slow down the processing). " +
                       "Set the setting in the XML project file 'includeNatural' to 'true' to include trees that died due to natural mortality, " +
                       "the setting 'includeHarvest' controls whether to include ('true') or exclude ('false') harvested trees. ");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.species());
            columns().Add(new OutputColumn("reason", "Resaon for tree death: 'N': Natural mortality, 'H': Harvest (removed from the forest), 'D': Disturbance (not salvage-harvested), 'S': Salvage harvesting (i.e. disturbed trees which are harvested), 'C': killed/cut down by management", OutputDatatype.OutString));
            columns().Add(new OutputColumn("count", "number of died trees (living, >4m height) ", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("volume_m3", "sum of volume (geomery, taper factor) in m3", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("basal_area_m2", "total basal area at breast height (m2)", OutputDatatype.OutDouble));

        }

        public void execRemovedTree(Tree t, int reason)
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

            int key = reason * 10000 + t.species().index();
            LROdata d = mLandscapeRemoval[key];
            d.basal_area += t.basalArea();
            d.volume += t.volume();
            d.n++;


        }

        public void exec()
        {
            foreach (KeyValuePair<int, LROdata> i in mLandscapeRemoval)
            {
                if (i.Value.n > 0)
                {
                    TreeRemovalType rem_type = (TreeRemovalType)(i.Key / 10000);
                    int species_index = i.Key % 10000;
                    this.add(currentYear());
                    this.add(GlobalSettings.instance().model().speciesSet().species(species_index).id());
                    if (rem_type == TreeRemovalType.TreeDeath) this.add("N");
                    if (rem_type == TreeRemovalType.TreeHarvest) this.add("H");
                    if (rem_type == TreeRemovalType.TreeDisturbance) this.add("D");
                    if (rem_type == TreeRemovalType.TreeSalavaged) this.add("S");
                    if (rem_type == TreeRemovalType.TreeCutDown) this.add("C");
                    this.add(i.Value.n);
                    this.add(i.Value.volume);
                    this.add(i.Value.basal_area);
                    writeRow();
                }
            }

            // clear data (no need to clear the hash table, right?)
            foreach (LROdata i in mLandscapeRemoval.Values)
            {
                i.clear();
            }
        }

        public void setup()
        {
            mIncludeHarvestTrees = settings().valueBool(".includeHarvest", true);
            mIncludeDeadTrees = settings().valueBool(".includeNatural", false);
            Tree.setLandscapeRemovalOutput(this);
        }
    }
}
