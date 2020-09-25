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
            this.mCondition = null;

            Name = "Detailed annual stand output (state).";
            TableName = "abeStandDetail";
            Description = "This output provides details about the forest state on species- and stand level. " +
                          "This output is more detailed than the abeStand output." + System.Environment.NewLine +
                          "The output is rather performance critical. You can use the ''condition'' XML-tag to limit the execution to certain years (e.g., mod(year,10)=1 ).";
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(OutputColumn.CreateSpecies());
            Columns.Add(new OutputColumn("standid", "unique identifier of the forest stand", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("basalarea", "basal area of the species(trees >4m) (m2/ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("relBasalarea", "relative basal area share of the species (trees >4m) (0..1)", OutputDatatype.OutDouble));
        }

        public override void Exec()
        {
            if (mCondition != null)
            {
                if (mCondition.Calculate(GlobalSettings.Instance.CurrentYear) == 0)
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

                    // loop over all species
                    for (int i = 0; i < stand.SpeciesCount(); ++i)
                    {
                        SSpeciesStand sss = stand.speciesData(i);
                        this.Add(CurrentYear());
                        this.Add(sss.species.ID);
                        this.Add(stand.id());
                        this.Add(sss.basalArea);
                        this.Add(sss.relBasalArea);
                        WriteRow();
                    }
                }
            }
        }

        public override void Setup()
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
