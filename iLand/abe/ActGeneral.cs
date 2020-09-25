using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    /** @class ActGeneral
        @ingroup abe
        The ActGeneral class is an all-purpose activity and implements no specific forest management activity.
        */
    internal class ActGeneral : Activity
    {
        private QJSValue mAction;

        public ActGeneral()
        {
        }

        public override string Type() { return "general"; }

        public override List<string> Info()
        {
            List<string> lines = base.Info();
            lines.Add("this is the 'general' activity; the activity is not scheduled. Use the action-slot to define what should happen.");
            return lines;
        }

        public override void Setup(QJSValue value)
        {
            base.Setup(value);
            // specific
            mAction = FMSTP.ValueFromJS(value, "action", "", "Activity of type 'general'.");
            if (!mAction.IsCallable())
            {
                throw new NotSupportedException("'general' activity has not a callable javascript 'action'.");
            }
        }

        public override bool Execute(FMStand stand)
        {
            FomeScript.SetExecutionContext(stand);
            if (FMSTP.verbose() || stand.TracingEnabled())
            {
                Debug.WriteLine(stand.context() + " activity 'general': execute of " + name());
            }

            QJSValue result = mAction.Call();
            if (result.IsError())
            {
                throw new NotSupportedException(String.Format("{0} Javascript error in 'general' activity '{2}': {1}", stand.context(), result.ToString(), name()));
            }
            return result.ToBool();
        }
    }
}
