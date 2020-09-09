using iLand.output;
using iLand.tools;
using System.Collections.Generic;

namespace iLand.abe.output
{
    internal class ABEStandDetailsOut : Output
    {
        private Expression mCondition;

        public ABEStandDetailsOut()
        {
            setName("Detailed annual stand output (state).", "abeStandDetail");
            setDescription("This output provides details about the forest state on species- and stand level. " +
                       "This output is more detailed than the abeStand output." + System.Environment.NewLine +
                       "The output is rather performance critical. You can use the ''condition'' XML-tag to limit the execution to certain years (e.g., mod(year,10)=1 ).");
            columns().Add(OutputColumn.year());
            columns().Add(OutputColumn.species());
            columns().Add(new OutputColumn("standid", "unique identifier of the forest stand", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("basalarea", "basal area of the species(trees >4m) (m2/ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("relBasalarea", "relative basal area share of the species (trees >4m) (0..1)", OutputDatatype.OutDouble));
        }

        public override void exec()
        {
            if (mCondition != null)
            {
                if (mCondition.calculate(GlobalSettings.instance().currentYear()) == 0)
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

                    // loop over all species
                    for (int i = 0; i < stand.nspecies(); ++i)
                    {
                        SSpeciesStand sss = stand.speciesData(i);
                        this.add(currentYear());
                        this.add(sss.species.id());
                        this.add(stand.id());
                        this.add(sss.basalArea);
                        this.add(sss.relBasalArea);
                        writeRow();
                    }
                }
            }
        }

        public void setup()
        {
            // use a condition for to control execuation for the current year
            string condition = settings().value(".condition", "");
            mCondition.setExpression(condition);
        }
    }
}
