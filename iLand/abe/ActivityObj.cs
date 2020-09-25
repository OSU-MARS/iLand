using System.Diagnostics;

namespace iLand.abe
{
    internal class ActivityObj
    {
        private int mActivityIndex; // link to base activity
        private Activity mActivity; // pointer
        private FMStand mStand; // and to the forest stand....

        public ActivityObj()
        {
            mActivityIndex = -1;
            mStand = null;
            mActivity = null;
        }

        /// set an activity that is not the current activity of the stand
        public void setActivityIndex(int index) { mActivityIndex = index; }
        public bool active() { return GetFlags().IsActive(); }
        public void setActive(bool activate) { GetFlags().SetIsActive(activate); }

        public bool finalHarvest() { return GetFlags().IsFinalHarvest(); }
        public void setFinalHarvest(bool isfinal) { GetFlags().SetIsFinalHarvest(isfinal); }

        public bool scheduled() { return GetFlags().IsScheduled(); }
        public void setScheduled(bool issched) { GetFlags().SetIsScheduled(issched); }

        // used to construct a link to a given activty (with an index that could be not the currently active index!)
        public ActivityObj(FMStand stand, Activity act, int index)
        {
            mActivityIndex = index;
            mStand = stand;
            mActivity = act;
        }

        public bool IsEnabled() { return GetFlags().IsEnabled(); }

        public string GetName() { return mActivity != null ? mActivity.name() : "undefined"; }

        public void SetIsEnabled(bool do_enable) { GetFlags().SetIsEnabled(do_enable); }

        /// default-case: set a forest stand as the context.
        public void SetStand(FMStand stand) { mStand = stand; mActivity = null; mActivityIndex = -1; }
        /// set an activity context (without a stand) to access base properties of activities
        public void SetActivity(Activity act) { mStand = null; mActivity = act; mActivityIndex = -1; }

        private ActivityFlags GetFlags()
        {
            // refer to a specific  activity of the stand (as returned by stand.activity("xxx") )
            if (mStand != null && mActivityIndex > -1)
            {
                return mStand.flags(mActivityIndex);
            }
            // refer to the *current* activity (the "activity" variable)
            if (mStand != null && mActivity == null)
            {
                return mStand.currentFlags();
            }
            // during setup of activites (onCreate-handler)
            if (mStand == null && mActivity != null)
            {
                return mActivity.mBaseActivity;
            }

            Debug.WriteLine("ActivityObj:flags: invalid access of flags! stand: " + mStand + " activity-index: " + mActivityIndex);
            return null;
        }
    }
}
