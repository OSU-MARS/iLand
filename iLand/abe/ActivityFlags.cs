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

        private readonly Activity mActivity; ///< link to activity
        private Flags mFlags;

        public Activity activity() { return mActivity; }

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

        public bool DoSimulate() { return GetFlag(Flags.DoSimulate); }
        public bool IsActive() { return GetFlag(Flags.Active); }
        public bool IsEnabled() { return GetFlag(Flags.Enabled); }
        public bool IsExecuteImmediate() { return GetFlag(Flags.ExecuteImmediate); }
        public bool IsFinalHarvest() { return GetFlag(Flags.FinalHarvest); }
        public bool IsForceNext() { return GetFlag(Flags.ExecuteNext); }
        public bool IsPending() { return GetFlag(Flags.Pending); }
        public bool IsRepeating() { return GetFlag(Flags.Repeater); }
        public bool IsSalvage() { return GetFlag(Flags.IsSalvage); }
        public bool IsScheduled() { return GetFlag(Flags.IsScheduled); }

        public void SetDoSimulate(bool dosimulate) { SetFlag(Flags.DoSimulate, dosimulate); }
        public void SetIsActive(bool active) { SetFlag(Flags.Active, active); }
        public void SetIsEnabled(bool enabled) { SetFlag(Flags.Enabled, enabled); }
        public void SetIsExecuteImmediate(bool doexec) { SetFlag(Flags.ExecuteImmediate, doexec); }
        public void SetIsFinalHarvest(bool isfinal) { SetFlag(Flags.FinalHarvest, isfinal); }
        public void SetIsForceNext(bool isnext) { SetFlag(Flags.ExecuteNext, isnext); }
        public void SetIsPending(bool pending) { SetFlag(Flags.Pending, pending); }
        public void SetIsRepeating(bool repeat) { SetFlag(Flags.Repeater, repeat); }
        public void SetIsSalvage(bool issalvage) { SetFlag(Flags.IsSalvage, issalvage); }
        public void SetIsScheduled(bool doschedule) { SetFlag(Flags.IsScheduled, doschedule); }

        private bool GetFlag(Flags flag) 
        { 
            return (mFlags & flag) != (Flags)0; 
        }

        private void SetFlag(Flags flag, bool value)
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
    }
}
