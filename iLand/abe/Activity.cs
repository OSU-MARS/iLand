using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    internal class Activity
    {
        protected static List<string> mAllowedProperties; // list of properties (e.g. 'schedule') that are parsed by the base activity

        public ActivityFlags mBaseActivity; // base properties of the activity (that can be changed for each stand)
        private readonly Constraints mConstraints; // constraining factors
        private readonly DynamicExpression mEnabledIf; // enabledIf property (dynamically evaluated)
        private readonly Events mEvents; // action handlers such as "onExecute"
        private int mIndex; ///< index of the activity within the STP
        private string mName; ///< the name of the activity;
        private readonly Schedule mSchedule; // timing of activity

        public Schedule schedule() { return mSchedule; }
        protected Constraints constraints() { return mConstraints; }
        public Events events() { return mEvents; }

        public void setIndex(int index) { mIndex = index; } // used during setup
        public void setName(string name) { mName = name; }

        public string name() { return mName; } ///< name of the activity as provided by JS
        public int index() { return mIndex; } ///< index of the activity within the STP
                                              /// get earlist possible scheduled year (relative to rotation begin)

        public Activity()
        {
            this.mBaseActivity = new ActivityFlags(this);
            this.mBaseActivity.SetIsActive(true);
            this.mBaseActivity.SetIsEnabled(true);
            this.mConstraints = new Constraints();
            this.mEnabledIf = new DynamicExpression();
            this.mEvents = new Events();
            this.mIndex = 0;
            this.mSchedule = new Schedule(new QJSValue());
        }

        public int GetEarlistSchedule(double U = 100.0) { return (int)mSchedule.MinValue(U); }
        /// get latest possible scheduled year (relative to rotation begin)
        public int GetLatestSchedule(double U = 100.0) { return (int)mSchedule.MaxValue(U); }
        /// get optimal scheduled year (relative to rotation begin)
        public int GetOptimalSchedule(double U = 100.0) { return (int)mSchedule.OptimalValue(U); }
        public bool IsRepeating() { return mSchedule.repeat; }

        public static Activity CreateActivity(string type)
        {
            return type switch
            {
                "general" => new ActGeneral(),
                "scheduled" => new ActScheduled(),
                "planting" => new ActPlanting(),
                "salvage" => new ActSalvage(),
                "thinning" => new ActThinning(),
                _ => throw new NotSupportedException(String.Format("Error: the activity type '{0}' is not a valid type.", type)),
            };
        }

        public virtual string Type()
        {
            return "base";
        }

        public virtual void Setup(QJSValue value)
        {
            mSchedule.Setup(FMSTP.ValueFromJS(value, "schedule", "", "setup activity"));
            if (FMSTP.verbose())
            {
                Debug.WriteLine(mSchedule.Dump());
            }
            // setup of events
            mEvents.Clear();
            mEvents.Setup(value, new List<string>() { "onCreate", "onSetup", "onEnter", "onExit", "onExecute", "onExecuted", "onCancel" });
            if (FMSTP.verbose())
            {
                Debug.WriteLine("Events: " + mEvents.Dump());
            }

            // setup of constraints
            QJSValue constraints = FMSTP.ValueFromJS(value, "constraint");
            if (!constraints.IsUndefined())
            {
                mConstraints.Setup(constraints);
            }
            // enabledIf property
            QJSValue enabled_if = FMSTP.ValueFromJS(value, "enabledIf");
            if (!enabled_if.IsUndefined())
            {
                mEnabledIf.Setup(enabled_if);
            }
        }

        public virtual double ScheduleProbability(FMStand stand, int specific_year = -1)
        {
            // return a value between 0 and 1; return -1 if the activity is expired.
            return schedule().Value(stand, specific_year);
        }

        public virtual double ExeceuteProbability(FMStand stand)
        {
            // check the standard constraints and return true when all constraints are fulfilled (or no constraints set)
            return constraints().Evaluate(stand);
        }

        public virtual bool Execute(FMStand stand)
        {
            // execute the "onExecute" event
            events().Run("onExecute", stand);
            return true;
        }

        public virtual bool Evaluate(FMStand stand)
        {
            // execute the "onEvaluate" event: the execution is canceled, if the function returns false.
            bool cancel = events().Run("onEvaluate", stand).ToBool();
            return !cancel;
        }

        public virtual void EvaluateDyanamicExpressions(FMStand stand)
        {
            // evaluate the enabled-if property and set the enabled flag of the stand (i.e. the ActivityFlags)
            if (mEnabledIf.IsValid())
            {
                bool result = mEnabledIf.Evaluate(stand);
                stand.flags(mIndex).SetIsEnabled(result);
            }
        }

        public virtual List<string> Info()
        {
            List<string> info = new List<string>() { String.Format("Activity '{0}': type '{1}'", name(), Type()),
                                                                   "Events", "-", events().Dump(), "/-",
                                                                   "Schedule", "-", schedule().Dump(), "/-",
                                                                   "Constraints", "-" };
            info.AddRange(constraints().Dump());
            info.Add("/-");
            return info;
        }

        public ActivityFlags StandFlags(FMStand stand = null)
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
