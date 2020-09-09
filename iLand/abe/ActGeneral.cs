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

        public ActGeneral(FMSTP parent)
            : base(parent)
        {
        }

        public override string type() { return "general"; }

        public override List<string> info()
        {
            List<string> lines = base.info();
            lines.Add("this is the 'general' activity; the activity is not scheduled. Use the action-slot to define what should happen.");
            return lines;
        }

        public new void setup(QJSValue value)
        {
            base.setup(value);
            // specific
            mAction = FMSTP.valueFromJs(value, "action", "", "Activity of type 'general'.");
            if (!mAction.isCallable())
            {
                throw new NotSupportedException("'general' activity has not a callable javascript 'action'.");
            }
        }

        public override bool execute(FMStand stand)
        {
            FomeScript.setExecutionContext(stand);
            if (FMSTP.verbose() || stand.trace())
            {
                Debug.WriteLine(stand.context() + " activity 'general': execute of " + name());
            }

            QJSValue result = mAction.call();
            if (result.isError())
            {
                throw new NotSupportedException(String.Format("{0} Javascript error in 'general' activity '{2}': {1}", stand.context(), result.toString(), name()));
            }
            return result.toBool();
        }

    }
}
