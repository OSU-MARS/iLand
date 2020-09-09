using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;

namespace iLand.abe
{
    internal class Activity
    {
        protected static List<string> mAllowedProperties; // list of properties (e.g. 'schedule') that are parsed by the base activity

        private int mIndex; ///< index of the activity within the STP
        private string mName; ///< the name of the activity;
        private FMSTP mProgram; // link to the management programme the activity is part of
        private Schedule mSchedule; // timing of activity
        private Constraints mConstraints; // constraining factors
        private Events mEvents; // action handlers such as "onExecute"
        private DynamicExpression mEnabledIf; // enabledIf property (dynamically evaluated)

        public ActivityFlags mBaseActivity; // base properties of the activity (that can be changed for each stand)

        public Schedule schedule() { return mSchedule; }
        protected Constraints constraints() { return mConstraints; }
        public Events events() { return mEvents; }

        public void setIndex(int index) { mIndex = index; } // used during setup
        public void setName(string name) { mName = name; }

        public string name() { return mName; } ///< name of the activity as provided by JS
        public int index() { return mIndex; } ///< index of the activity within the STP
                                              /// get earlist possible scheduled year (relative to rotation begin)
        public int earlistSchedule(double U = 100.0) { return (int)mSchedule.minValue(U); }
        /// get latest possible scheduled year (relative to rotation begin)
        public int latestSchedule(double U = 100.0) { return (int)mSchedule.maxValue(U); }
        /// get optimal scheduled year (relative to rotation begin)
        public int optimalSchedule(double U = 100.0) { return (int)mSchedule.optimalValue(U); }
        public bool isRepeatingActivity() { return mSchedule.repeat; }

        public Activity(FMSTP parent)
        {
            mProgram = parent;
            mIndex = 0;
            mBaseActivity = new ActivityFlags(this);
            mBaseActivity.setActive(true);
            mBaseActivity.setEnabled(true);
        }

        public static Activity createActivity(string type, FMSTP stp)
        {
            return type switch
            {
                "general" => new ActGeneral(stp),
                "scheduled" => new ActScheduled(stp),
                "planting" => new ActPlanting(stp),
                "salvage" => new ActSalvage(stp),
                "thinning" => new ActThinning(stp),
                _ => throw new NotSupportedException(String.Format("Error: the activity type '{0}' is not a valid type.", type)),
            };
        }

        public virtual string type()
        {
            return "base";
        }

        public void setup(QJSValue value)
        {
            mSchedule.setup(FMSTP.valueFromJs(value, "schedule", "", "setup activity"));
            if (FMSTP.verbose())
            {
                Debug.WriteLine(mSchedule.dump());
            }
            // setup of events
            mEvents.clear();
            mEvents.setup(value, new List<string>() { "onCreate", "onSetup", "onEnter", "onExit", "onExecute", "onExecuted", "onCancel" });
            if (FMSTP.verbose())
            {
                Debug.WriteLine("Events: " + mEvents.dump());
            }

            // setup of constraints
            QJSValue constraints = FMSTP.valueFromJs(value, "constraint");
            if (!constraints.isUndefined())
            {
                mConstraints.setup(constraints);
            }
            // enabledIf property
            QJSValue enabled_if = FMSTP.valueFromJs(value, "enabledIf");
            if (!enabled_if.isUndefined())
            {
                mEnabledIf.setup(enabled_if);
            }
        }

        public virtual double scheduleProbability(FMStand stand, int specific_year = -1)
        {
            // return a value between 0 and 1; return -1 if the activity is expired.
            return schedule().value(stand, specific_year);
        }

        public virtual double execeuteProbability(FMStand stand)
        {
            // check the standard constraints and return true when all constraints are fulfilled (or no constraints set)
            return constraints().evaluate(stand);
        }

        public virtual bool execute(FMStand stand)
        {
            // execute the "onExecute" event
            events().run("onExecute", stand);
            return true;
        }

        public virtual bool evaluate(FMStand stand)
        {
            // execute the "onEvaluate" event: the execution is canceled, if the function returns false.
            bool cancel = events().run("onEvaluate", stand).toBool();
            return !cancel;
        }

        public virtual void evaluateDyanamicExpressions(FMStand stand)
        {
            // evaluate the enabled-if property and set the enabled flag of the stand (i.e. the ActivityFlags)
            if (mEnabledIf.isValid())
            {
                bool result = mEnabledIf.evaluate(stand);
                stand.flags(mIndex).setEnabled(result);
            }
        }

        public virtual List<string> info()
        {
            List<string> info = new List<string>() { String.Format("Activity '{0}': type '{1}'", name(), type()),
                                                                   "Events", "-", events().dump(), "/-",
                                                                   "Schedule", "-", schedule().dump(), "/-",
                                                                   "Constraints", "-" };
            info.AddRange(constraints().dump());
            info.Add("/-");
            return info;
        }

        public ActivityFlags standFlags(FMStand stand = null)
        {
            // use the base data item if no specific stand is provided
            if (stand == null)
            {
                return mBaseActivity;
            }
            // return the flags associated with the specific stand
            return stand.flags(mIndex);
        }
    }
}
