namespace iLand.abe
{
    internal class SchedulerObj
    {
        private FMStand mStand; // link to the forest stand
        public void setStand(FMStand stand) { mStand = stand; }

        public SchedulerObj()
        {
            mStand = null;
        }

        public void Dump()
        {
            if (mStand == null || mStand.unit() == null || mStand.unit().constScheduler() == null)
            {
                return;
            }
            mStand.unit().constScheduler().Dump();
        }

        public bool Enabled()
        {
            if (mStand == null)
            {
                return false;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            return opt.useScheduler;
        }

        public void SetEnabled(bool is_enabled)
        {
            if (mStand == null)
            {
                return;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            opt.useScheduler = is_enabled;
        }

        public double harvestIntensity()
        {
            if (mStand == null)
            {
                return 0.0;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            return opt.harvestIntensity;
        }

        public void SetHarvestIntensity(double new_intensity)
        {
            if (mStand == null)
            {
                return;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            opt.harvestIntensity = new_intensity;

        }

        public double UseSustainableHarvest()
        {
            if (mStand == null)
            {
                return 0.0;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            return opt.useSustainableHarvest;
        }

        public void SetUseSustainableHarvest(double new_level)
        {
            if (mStand == null)
            {
                return;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            opt.useSustainableHarvest = new_level;
        }

        public double MaxHarvestLevel()
        {
            if (mStand == null)
            {
                return 0.0;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            return opt.maxHarvestLevel;
        }

        public void SetMaxHarvestLevel(double new_harvest_level)
        {
            if (mStand == null)
            {
                return;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            opt.maxHarvestLevel = new_harvest_level;
        }
    }
}
