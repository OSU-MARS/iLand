namespace iLand.abe
{
    internal class SchedulerObj
    {
        private FMStand mStand; // link to the forest stand
        public void setStand(FMStand stand) { mStand = stand; }

        public SchedulerObj(object parent = null)
        {
            mStand = null;
        }

        public void dump()
        {
            if (mStand == null || mStand.unit() == null || mStand.unit().constScheduler() == null)
            {
                return;
            }
            mStand.unit().constScheduler().dump();
        }

        public bool enabled()
        {
            if (mStand == null)
            {
                return false;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            return opt.useScheduler;
        }

        public void setEnabled(bool is_enabled)
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

        public void setHarvestIntensity(double new_intensity)
        {
            if (mStand == null)
            {
                return;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            opt.harvestIntensity = new_intensity;

        }

        public double useSustainableHarvest()
        {
            if (mStand == null)
            {
                return 0.0;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            return opt.useSustainableHarvest;
        }

        public void setUseSustainableHarvest(double new_level)
        {
            if (mStand == null)
            {
                return;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            opt.useSustainableHarvest = new_level;
        }

        public double maxHarvestLevel()
        {
            if (mStand == null)
            {
                return 0.0;
            }
            SchedulerOptions opt = mStand.unit().agent().schedulerOptions();
            return opt.maxHarvestLevel;
        }

        public void setMaxHarvestLevel(double new_harvest_level)
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
