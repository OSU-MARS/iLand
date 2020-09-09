using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    internal class SchedulerOptions
    {
        private static readonly List<string> mAllowedProperties = new List<string>() {
                        "minScheduleHarvest", "maxScheduleHarvest", "maxHarvestLevel",
                        "useSustainableHarvest", "scheduleRebounceDuration", "deviationDecayRate",
                        "enabled", "harvestIntensity" };

        public bool useScheduler; ///< true, if the agent is using the scheduler at all
        public double useSustainableHarvest; ///< scaling factor (0..1), 1 if scheduler used by agent (exclusively), 0: bottom up, linearly scaled in between.
        public double minScheduleHarvest; ///< minimum amount of m3/ha*yr that should be scheduled
        public double maxScheduleHarvest; ///< the maximum number of m3/ha*yr that should be scheduled
        public double maxHarvestLevel; ///< multiplier to define the maximum overshoot over the planned volume (e.g. 1.2 -> 20% max. overshoot)
        public double harvestIntensity; ///< multiplier for the "sustainable" harvest level
        public double scheduleRebounceDuration; ///< number of years for which deviations from the planned volume are split into
        public double deviationDecayRate; ///< factor to reduce accumulated harvest deviation

        public SchedulerOptions()
        {
            useScheduler = false; 
            useSustainableHarvest = 1.0; 
            minScheduleHarvest = 0; 
            maxScheduleHarvest = 0; 
            maxHarvestLevel = 0; 
            harvestIntensity = 1.0; 
            scheduleRebounceDuration = 0; 
            deviationDecayRate = 0.0;
        }

        public void setup(QJSValue jsvalue)
        {
            useScheduler = false;
            if (!jsvalue.isObject())
            {
                Debug.WriteLine("Scheduler options are not an object: " + jsvalue.toString());
                return;
            }
            FMSTP.checkObjectProperties(jsvalue, mAllowedProperties, "setup of scheduler options");

            minScheduleHarvest = FMSTP.valueFromJs(jsvalue, "minScheduleHarvest", "0").toNumber();
            maxScheduleHarvest = FMSTP.valueFromJs(jsvalue, "maxScheduleHarvest", "10000").toNumber();
            maxHarvestLevel = FMSTP.valueFromJs(jsvalue, "maxHarvestLevel", "2").toNumber();
            Debug.WriteLine("maxHarvestLevel " + maxHarvestLevel);
            useSustainableHarvest = FMSTP.valueFromJs(jsvalue, "useSustainableHarvest", "1").toNumber();
            if (useSustainableHarvest < 0.0 || useSustainableHarvest > 1.0)
            {
                throw new NotSupportedException("Setup of scheduler-options: invalid value for 'useSustainableHarvest' (0..1 allowed).");
            }

            harvestIntensity = FMSTP.valueFromJs(jsvalue, "harvestIntensity", "1").toNumber();
            scheduleRebounceDuration = FMSTP.valueFromJs(jsvalue, "scheduleRebounceDuration", "5").toNumber();
            if (scheduleRebounceDuration == 0.0)
            {
                throw new NotSupportedException("Setup of scheduler-options: '0' is not a valid value for 'scheduleRebounceDuration'!");
            }
            // calculate the "tau" of a exponential decay function based on the provided half-time
            scheduleRebounceDuration = scheduleRebounceDuration / Math.Log(2.0);
            deviationDecayRate = FMSTP.valueFromJs(jsvalue, "deviationDecayRate", "0").toNumber();
            if (deviationDecayRate == 1.0)
            {
                throw new NotSupportedException("Setup of scheduler-options: '0' is not a valid value for 'deviationDecayRate'!");
            }
            deviationDecayRate = 1.0 - deviationDecayRate; // if eg value is 0.05 -> multiplier 0.95
            useScheduler = FMSTP.boolValueFromJs(jsvalue, "enabled", true);
        }
    }
}
