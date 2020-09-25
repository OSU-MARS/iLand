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
        public override string Type()
        {
            return "scheduled";
        }

        public ActScheduled()
        {
            mBaseActivity.SetIsScheduled(true); // use the scheduler
            mBaseActivity.SetDoSimulate(true); // simulate per default
        }

        public override void Setup(QJSValue value)
        {
            base.Setup(value);
            events().Setup(value, new List<string>() { "onEvaluate" });

            if (!events().HasEvent("onEvaluate"))
            {
                throw new NotSupportedException("activity %1 (of type 'scheduled') requires to have the 'onSchedule' event.");
            }
        }

        public override bool Execute(FMStand stand)
        {
            if (events().HasEvent("onExecute"))
            {
                // switch off simulation mode
                stand.currentFlags().SetDoSimulate(false);
                // execute this event
                bool result = base.Execute(stand);
                stand.currentFlags().SetDoSimulate(true);
                return result;
            }
            else
            {
                // default behavior: process all marked trees (harvest / cut)
                if (stand.TracingEnabled()) Debug.WriteLine(stand.context() + " activity " + name() + " remove all marked trees.");
                FMTreeList trees = new FMTreeList(stand);
                trees.RemoveMarkedTrees();
                return true;
            }
        }

        public override bool Evaluate(FMStand stand)
        {
            // this is called when it should be tested
            stand.currentFlags().SetDoSimulate(true);
            QJSValue result = events().Run("onEvaluate", stand);
            if (stand.TracingEnabled())
            {
                Debug.WriteLine(stand.context() + " executed onEvaluate event of " + name() + " with result: " + FomeScript.JStoString(result));
            }

            if (result.IsNumber())
            {
                double harvest = result.ToNumber();

                // the return value is interpreted as scheduled harvest; if this value is 0, then no
                if (harvest == 0.0)
                {
                    return false;
                }
                stand.AddScheduledHarvest(harvest);
                if (stand.TracingEnabled())
                {
                    Debug.WriteLine(stand.context() + "scheduled harvest is now" + stand.scheduledHarvest());
                }
                return true;
            }
            bool bool_result = result.ToBool();
            return bool_result;
        }

        public override List<string> Info()
        {
            List<string> lines = base.Info();
            //lines.Add("this is an activity of type 'scheduled'.");
            return lines;
        }
    }
}
