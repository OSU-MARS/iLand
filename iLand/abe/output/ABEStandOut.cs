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
            setName("Annual stand output (state).", "abeStand");
            setDescription("This output provides details about the forest state on stand level. " +
                       "The timber is provided as standing timber per hectare." + System.Environment.NewLine +
                       "The output is rather performance critical. You can use the ''condition'' XML-tag to limit the execution to certain years (e.g., mod(year,10)=1 ).");
            columns().Add(OutputColumn.year());
            columns().Add(new OutputColumn("unitid", "unique identifier of the planning unit", OutputDatatype.OutString));
            columns().Add(new OutputColumn("standid", "unique identifier of the forest stand", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("initialstandid", "stand id if not split, stand id of the source stand after splitting a stand.", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("area", "total area of the forest stand (ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("volume", "standing timber volume (after harvests of the year) (m3/ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("basalarea", "basal area (trees >4m) (m2/ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("dbh", "mean diameter (basal area weighted, of trees >4m) (cm)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("height", "mean stand tree height (basal area weighted, of trees >4m)(cm)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("stems", "number of trees (trees >4m) per ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("age", "the age of the stand (years since beginning of the rotation)", OutputDatatype.OutDouble));
        }

        public override void exec()
        {
            if (mCondition != null)
            {
                if (mCondition.calculate(GlobalSettings.instance().currentYear()) == 0.0)
                {
                    return;
                }
            }

            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> unit in ForestManagementEngine.instance().stands())
            {
                foreach (FMStand stand in unit.Value)
                {
                    // Note: EXPENSIVE reload operation for every stand and every year....
                    stand.reload();

                    this.add(currentYear());
                    this.add(stand.unit().id());
                    this.add(stand.id());
                    this.add(stand.initialStandId());
                    this.add(stand.area());
                    this.add(Math.Round(stand.volume() * 100.0) / 100.0);
                    this.add(Math.Round(stand.basalArea() * 100.0) / 100.0);
                    this.add(Math.Round(stand.dbh() * 100.0) / 100.0);
                    this.add(Math.Round(stand.height() * 100.0) / 100.0);
                    this.add(Math.Round(stand.stems()));
                    this.add(stand.absoluteAge());
                    writeRow();
                }
            }
        }

        public new void setup()
        {
            // use a condition for to control execuation for the current year
            string condition = settings().value(".condition", "");
            mCondition.setExpression(condition);
        }
    }
}
