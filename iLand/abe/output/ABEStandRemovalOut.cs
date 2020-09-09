using iLand.output;
using System;
using System.Collections.Generic;

namespace iLand.abe.output
{
    internal class ABEStandRemovalOut : Output
    {
        public ABEStandRemovalOut()
        {
            setName("Annual harvests on stand level.", "abeStandRemoval");
            setDescription("This output provides details about realized timber harvests on stand level. " +
                       "The timber is provided as standing timber per hectare. The total harvest on the stand is the sum of thinning and final.");
            columns().Add(OutputColumn.year());
            columns().Add(new OutputColumn("unitid", "unique identifier of the planning unit", OutputDatatype.OutString));
            columns().Add(new OutputColumn("standid", "unique identifier of the forest stand", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("area", "total area of the forest stand (ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("age", "absolute stand age at the time of the activity (yrs)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("activity", "name of the management activity that is executed", OutputDatatype.OutString));
            columns().Add(new OutputColumn("volumeAfter", "standing timber volume after the harvest operation (m3/ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("volumeThinning", "removed timber volume due to thinning, m3/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("volumeFinal", "removed timber volume due to final harvests (regeneration cuts) and due to salvage operations, m3/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("volumeDisturbed", "disturbed trees on the stand, m3/ha. Note: all killed trees are recorded here,also those trees that are not salvaged (due to size and other constraints)", OutputDatatype.OutDouble));
        }

        public override void exec()
        {
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> unit in ForestManagementEngine.instance().stands())
            {
                foreach (FMStand stand in unit.Value)
                {
                    if (stand.totalHarvest() > 0.0)
                    {
                        this.add(currentYear());
                        this.add(stand.unit().id());
                        this.add(stand.id());
                        this.add(stand.area());
                        this.add(stand.lastExecutionAge());
                        this.add(stand.lastExecutedActivity() != null ? stand.lastExecutedActivity().name() : null);
                        this.add(Math.Round(stand.volume() * 100.0) / 100.0);
                        this.add(stand.totalThinningHarvest() / stand.area()); //  thinning alone
                        this.add((stand.totalHarvest() - stand.totalThinningHarvest()) / stand.area()); // final harvests (including salvage operations)
                        this.add(stand.disturbedTimber() / stand.area());  // disturbed trees on the stand

                        writeRow();
                    }
                }
            }
        }
    }
}
