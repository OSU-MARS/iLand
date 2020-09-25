using iLand.output;
using System;
using System.Collections.Generic;

namespace iLand.abe.output
{
    internal class ABEStandRemovalOut : Output
    {
        public ABEStandRemovalOut()
        {
            Name = "Annual harvests on stand level.";
            TableName = "abeStandRemoval";
            Description = "This output provides details about realized timber harvests on stand level. " +
                          "The timber is provided as standing timber per hectare. The total harvest on the stand is the sum of thinning and final.";
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(new OutputColumn("unitid", "unique identifier of the planning unit", OutputDatatype.OutString));
            Columns.Add(new OutputColumn("standid", "unique identifier of the forest stand", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("area", "total area of the forest stand (ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("age", "absolute stand age at the time of the activity (yrs)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("activity", "name of the management activity that is executed", OutputDatatype.OutString));
            Columns.Add(new OutputColumn("volumeAfter", "standing timber volume after the harvest operation (m3/ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("volumeThinning", "removed timber volume due to thinning, m3/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("volumeFinal", "removed timber volume due to final harvests (regeneration cuts) and due to salvage operations, m3/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("volumeDisturbed", "disturbed trees on the stand, m3/ha. Note: all killed trees are recorded here,also those trees that are not salvaged (due to size and other constraints)", OutputDatatype.OutDouble));
        }

        public override void Exec()
        {
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> unit in ForestManagementEngine.instance().stands())
            {
                foreach (FMStand stand in unit.Value)
                {
                    if (stand.TotalHarvest() > 0.0)
                    {
                        this.Add(CurrentYear());
                        this.Add(stand.unit().id());
                        this.Add(stand.id());
                        this.Add(stand.area());
                        this.Add(stand.LastExecutionAge());
                        this.Add(stand.LastExecutedActivity()?.name());
                        this.Add(Math.Round(stand.volume() * 100.0) / 100.0);
                        this.Add(stand.totalThinningHarvest() / stand.area()); //  thinning alone
                        this.Add((stand.TotalHarvest() - stand.totalThinningHarvest()) / stand.area()); // final harvests (including salvage operations)
                        this.Add(stand.disturbedTimber() / stand.area());  // disturbed trees on the stand

                        WriteRow();
                    }
                }
            }
        }
    }
}
