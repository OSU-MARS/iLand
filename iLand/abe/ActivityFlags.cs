using System;

namespace iLand.abe
{
    internal class ActivityFlags
    {
        /// (binary coded)  flags
        [Flags]
        private enum Flags
        {
            Active = 1,  // if false, the activity has already been executed
            Enabled = 2,  // if false, the activity can not be executed
            Repeater = 4, // if true, the activity is executed
            ExecuteNext = 8, // this activity should be executed next (kind of "goto"
            ExecuteImmediate = 16, // should be executed immediately by the scheduler (e.g. required sanitary cuttings)
            Pending = 32,  // the activity is currently in the scheduling algorithm
            FinalHarvest = 64,  // the management of the activity is a "endnutzung" (compared to "vornutzung")
            IsScheduled = 128, // the execution time of the activity is scheduled by the Scheduler component
            DoSimulate = 256,  // the default operation mode of harvests (simulate or not)
            IsSalvage = 512   // the activity is triggered by tree mortality events
        };

        private Activity mActivity; ///< link to activity
        private Flags mFlags;

        public Activity activity() { return mActivity; }
        private bool flag(Flags flag) { return (mFlags & flag) != (Flags)0; }
        private void setFlag(Flags flag, bool value)
        {
            if (value)
            {
                mFlags |= flag;
            }
            else
            {
                mFlags &= (flag ^ (Flags)0xffffff);
            }
        }

        public ActivityFlags()
        {
            mActivity = null;
            mFlags = 0;
        }

        public ActivityFlags(Activity act)
        {
            mActivity = act;
            mFlags = 0;
        }

        public bool active() { return flag(Flags.Active); }
        public bool enabled() { return flag(Flags.Enabled); }
        public bool isRepeating() { return flag(Flags.Repeater); }
        public bool isPending() { return flag(Flags.Pending); }
        public bool isForcedNext() { return flag(Flags.ExecuteNext); }
        public bool isFinalHarvest() { return flag(Flags.FinalHarvest); }
        public bool isExecuteImmediate() { return flag(Flags.ExecuteImmediate); }
        public bool isScheduled() { return flag(Flags.IsScheduled); }
        public bool isDoSimulate() { return flag(Flags.DoSimulate); }
        public bool isSalvage() { return flag(Flags.IsSalvage); }

        public void setActive(bool active) { setFlag(Flags.Active, active); }
        public void setEnabled(bool enabled) { setFlag(Flags.Enabled, enabled); }
        public void setIsRepeating(bool repeat) { setFlag(Flags.Repeater, repeat); }
        public void setIsPending(bool pending) { setFlag(Flags.Pending, pending); }
        public void setForceNext(bool isnext) { setFlag(Flags.ExecuteNext, isnext); }
        public void setFinalHarvest(bool isfinal) { setFlag(Flags.FinalHarvest, isfinal); }
        public void setExecuteImmediate(bool doexec) { setFlag(Flags.ExecuteImmediate, doexec); }
        public void setIsScheduled(bool doschedule) { setFlag(Flags.IsScheduled, doschedule); }
        public void setDoSimulate(bool dosimulate) { setFlag(Flags.DoSimulate, dosimulate); }
        public void setIsSalvage(bool issalvage) { setFlag(Flags.IsSalvage, issalvage); }
    }
}
