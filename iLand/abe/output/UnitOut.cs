using iLand.output;

namespace iLand.abe.output
{
    internal class UnitOut : Output
    {
        public UnitOut()
        {
            Name = "Annual harvests and harvest plan on unit level.";
            TableName = "abeUnit";
            Description = "The output provides planned and realized harvests on the level of planning units. " +
                          "Note that the planning unit area, mean age, mean volume and MAI are only updated every 10 years. " +
                          "Harvested timber is given as 'realizedHarvest', which is the sum of 'finalHarvest' and 'thinningHarvest.' " +
                          "The 'salvageHarvest' is provided extra, but already accounted for in the 'finalHarvest' column";
            Columns.Add(OutputColumn.CreateYear());
            Columns.Add(new OutputColumn("id", "unique identifier of the planning unit", OutputDatatype.OutString));
            Columns.Add(new OutputColumn("area", "total area of the unit (ha)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("age", "mean stand age (area weighted) (updated every 10yrs)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("U", "default rotation length for stands of the unit (years)", OutputDatatype.OutInteger));
            Columns.Add(new OutputColumn("thinningIntensity", "default thinning intensity for the unit", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("volume", "mean standing volume (updated every 10yrs), m3/ha", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("MAI", "mean annual increment (updated every 10yrs), m3/ha*yr", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("decadePlan", "planned mean harvest per year for the decade (m3/ha*yr)", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("annualPlan", "updated annual plan for the year, m3/ha*yr", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("runningDelta", "current aggregated difference between planned and realied harvests; positive: more realized than planned harvests, m3/ha*yr", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("realizedHarvest", "total harvested timber volume, m3/ha*yr", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("finalHarvest", "total harvested timber of planned final harvests (including salvage harvests), m3/ha*yr", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("thinningHarvest", "total harvested timber due to tending and thinning operations, m3/ha*yr", OutputDatatype.OutDouble));
            Columns.Add(new OutputColumn("salvageHarvest", "total harvested timber due to salvage operations (also included in final harvests), m3/ha*yr", OutputDatatype.OutDouble));
        }


        public override void Exec()
        {
            double salvage_harvest, annual_target;
            foreach (FMUnit unit in ForestManagementEngine.instance().units())
            {
                this.Add(CurrentYear());
                this.Add(unit.id()); // keys
                this.Add(unit.area());
                this.Add(unit.mMeanAge);
                this.Add(unit.U());
                this.Add(unit.thinningIntensity());
                this.Add(unit.mTotalVolume / unit.area());
                this.Add(unit.mMAI);
                this.Add(unit.mAnnualHarvestTarget);
                if (unit.scheduler() != null)
                {
                    salvage_harvest = unit.scheduler().mExtraHarvest / unit.area();
                    annual_target = unit.scheduler().mFinalCutTarget;
                }
                else
                {
                    salvage_harvest = 0.0;
                    annual_target = 0.0;

                }
                double thin_h = unit.AnnualThinningHarvest() / unit.area();
                this.Add(annual_target);
                this.Add(unit.mTotalPlanDeviation);
                this.Add(unit.GetAnnualTotalHarvest() / unit.area()); // total realized
                this.Add(unit.GetAnnualTotalHarvest() / unit.area() - thin_h); // final
                this.Add(thin_h); // thinning
                this.Add(salvage_harvest); // salvaging

                WriteRow();
            }
        }
    }
}
