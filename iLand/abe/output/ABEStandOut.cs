using iLand.output;
using iLand.tools;
using System;
using System.Collections.Generic;

namespace iLand.abe.output
{
    internal class ABEStandOut : Output
    {
        private Expression mCondition;

        public ABEStandOut()
        {
            this.mCondition = null;

            Name = "Annual stand output (state).";
            TableName = "abeStand";
            Description = "This output provides details about the forest state on stand level. " +
                          "The timber is provided as standing timber per hectare." + System.Environment.NewLine +
                          "The output is rather performance critical. You can use the ''condition'' XML-tag to limit the execution to certain years (e.g., mod(year,10)=1 ).";
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(new OutputColumn("unitid", "unique identifier of the planning unit", OutputDatatype.OutString));
            Columns.Add(new OutputColumn("standid", "unique identifier of the forest stand", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("initialstandid", "stand id if not split, stand id of the source stand after splitting a stand.", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("area", "total area of the forest stand (ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("volume", "standing timber volume (after harvests of the year) (m3/ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("basalarea", "basal area (trees >4m) (m2/ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("dbh", "mean diameter (basal area weighted, of trees >4m) (cm)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("height", "mean stand tree height (basal area weighted, of trees >4m)(cm)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("stems", "number of trees (trees >4m) per ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("age", "the age of the stand (years since beginning of the rotation)", OutputDatatype.OutDouble));
        }

        public override void Exec()
        {
            if (mCondition != null)
            {
                if (mCondition.Calculate(GlobalSettings.Instance.CurrentYear) == 0.0)
                {
                    return;
                }
            }

            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> unit in ForestManagementEngine.instance().stands())
            {
                foreach (FMStand stand in unit.Value)
                {
                    // Note: EXPENSIVE reload operation for every stand and every year....
                    stand.Reload();

                    this.Add(CurrentYear());
                    this.Add(stand.unit().id());
                    this.Add(stand.id());
                    this.Add(stand.initialStandId());
                    this.Add(stand.area());
                    this.Add(Math.Round(stand.volume() * 100.0) / 100.0);
                    this.Add(Math.Round(stand.basalArea() * 100.0) / 100.0);
                    this.Add(Math.Round(stand.dbh() * 100.0) / 100.0);
                    this.Add(Math.Round(stand.height() * 100.0) / 100.0);
                    this.Add(Math.Round(stand.stems()));
                    this.Add(stand.AbsoluteAge());
                    WriteRow();
                }
            }
        }

        public new void Setup()
        {
            if (this.mCondition == null)
            {
                this.mCondition = new Expression();
            }

            // use a condition for to control execuation for the current year
            string condition = Settings().Value(".condition", "");
            mCondition.SetExpression(condition);
        }
    }
}
