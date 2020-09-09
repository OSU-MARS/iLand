using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    /** @class ActScheduled
        @ingroup abe
        The ActScheduled class is an all-purpose activity (similar to ActGeneral). The execution time of ActScheduled-activities, however,
        is defined by the ABE Scheduler.
        */
    internal class ActScheduled : Activity
    {
        public override string type()
        {
            return "scheduled";
        }

        public ActScheduled(FMSTP parent)
                : base(parent)
        {
            mBaseActivity.setIsScheduled(true); // use the scheduler
            mBaseActivity.setDoSimulate(true); // simulate per default
        }

        public new void setup(QJSValue value)
        {
            base.setup(value);
            events().setup(value, new List<string>() { "onEvaluate" });

            if (!events().hasEvent("onEvaluate"))
            {
                throw new NotSupportedException("activity %1 (of type 'scheduled') requires to have the 'onSchedule' event.");
            }
        }

        public override bool execute(FMStand stand)
        {
            if (events().hasEvent("onExecute"))
            {
                // switch off simulation mode
                stand.currentFlags().setDoSimulate(false);
                // execute this event
                bool result = base.execute(stand);
                stand.currentFlags().setDoSimulate(true);
                return result;
            }
            else
            {
                // default behavior: process all marked trees (harvest / cut)
                if (stand.trace()) Debug.WriteLine(stand.context() + " activity " + name() + " remove all marked trees.");
                FMTreeList trees = new FMTreeList(stand);
                trees.removeMarkedTrees();
                return true;
            }
        }

        bool evaluate(FMStand stand)
        {
            // this is called when it should be tested
            stand.currentFlags().setDoSimulate(true);
            QJSValue result = events().run("onEvaluate", stand);
            if (stand.trace())
            {
                Debug.WriteLine(stand.context() + " executed onEvaluate event of " + name() + " with result: " + FomeScript.JStoString(result));
            }

            if (result.isNumber())
            {
                double harvest = result.toNumber();

                // the return value is interpreted as scheduled harvest; if this value is 0, then no
                if (harvest == 0.0)
                {
                    return false;
                }
                stand.addScheduledHarvest(harvest);
                if (stand.trace())
                {
                    Debug.WriteLine(stand.context() + "scheduled harvest is now" + stand.scheduledHarvest());
                }
                return true;
            }
            bool bool_result = result.toBool();
            return bool_result;
        }

        public override List<string> info()
        {
            List<string> lines = base.info();
            //lines.Add("this is an activity of type 'scheduled'.");
            return lines;
        }
    }
}
