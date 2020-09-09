using iLand.output;

namespace iLand.abe.output
{
    internal class UnitOut : Output
    {
        public UnitOut()
        {
            setName("Annual harvests and harvest plan on unit level.", "abeUnit");
            setDescription("The output provides planned and realized harvests on the level of planning units. " +
                       "Note that the planning unit area, mean age, mean volume and MAI are only updated every 10 years. " +
                       "Harvested timber is given as 'realizedHarvest', which is the sum of 'finalHarvest' and 'thinningHarvest.' " +
                       "The 'salvageHarvest' is provided extra, but already accounted for in the 'finalHarvest' column");
            columns().Add(OutputColumn.year());
            columns().Add(new OutputColumn("id", "unique identifier of the planning unit", OutputDatatype.OutString));
            columns().Add(new OutputColumn("area", "total area of the unit (ha)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("age", "mean stand age (area weighted) (updated every 10yrs)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("U", "default rotation length for stands of the unit (years)", OutputDatatype.OutInteger));
            columns().Add(new OutputColumn("thinningIntensity", "default thinning intensity for the unit", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("volume", "mean standing volume (updated every 10yrs), m3/ha", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("MAI", "mean annual increment (updated every 10yrs), m3/ha*yr", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("decadePlan", "planned mean harvest per year for the decade (m3/ha*yr)", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("annualPlan", "updated annual plan for the year, m3/ha*yr", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("runningDelta", "current aggregated difference between planned and realied harvests; positive: more realized than planned harvests, m3/ha*yr", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("realizedHarvest", "total harvested timber volume, m3/ha*yr", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("finalHarvest", "total harvested timber of planned final harvests (including salvage harvests), m3/ha*yr", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("thinningHarvest", "total harvested timber due to tending and thinning operations, m3/ha*yr", OutputDatatype.OutDouble));
            columns().Add(new OutputColumn("salvageHarvest", "total harvested timber due to salvage operations (also included in final harvests), m3/ha*yr", OutputDatatype.OutDouble));
        }


        public void exec()
        {
            double salvage_harvest, annual_target;
            foreach (FMUnit unit in ForestManagementEngine.instance().units())
            {
                this.add(currentYear());
                this.add(unit.id()); // keys
                this.add(unit.area());
                this.add(unit.mMeanAge);
                this.add(unit.U());
                this.add(unit.thinningIntensity());
                this.add(unit.mTotalVolume / unit.area());
                this.add(unit.mMAI);
                this.add(unit.mAnnualHarvestTarget);
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
                double thin_h = unit.annualThinningHarvest() / unit.area();
                this.add(annual_target);
                this.add(unit.mTotalPlanDeviation);
                this.add(unit.annualTotalHarvest() / unit.area()); // total realized
                this.add(unit.annualTotalHarvest() / unit.area() - thin_h); // final
                this.add(thin_h); // thinning
                this.add(salvage_harvest); // salvaging

                writeRow();
            }
        }
    }
}
