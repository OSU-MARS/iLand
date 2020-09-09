using System.Diagnostics;

namespace iLand.abe
{
    internal class ActivityObj
    {
        private static ActivityFlags mEmptyFlags;

        private int mActivityIndex; // link to base activity
        private Activity mActivity; // pointer
        private FMStand mStand; // and to the forest stand....

        public ActivityObj(object parent = null)
        {
            mActivityIndex = -1;
            mStand = null;
            mActivity = null;
        }

        // used to construct a link to a given activty (with an index that could be not the currently active index!)
        public ActivityObj(FMStand stand, Activity act, int index)
        {
            mActivityIndex = index;
            mStand = stand;
            mActivity = act;
        }

        /// default-case: set a forest stand as the context.
        public void setStand(FMStand stand) { mStand = stand; mActivity = null; mActivityIndex = -1; }
        /// set an activity context (without a stand) to access base properties of activities
        public void setActivity(Activity act) { mStand = null; mActivity = act; mActivityIndex = -1; }

        /// set an activity that is not the current activity of the stand
        public void setActivityIndex(int index) { mActivityIndex = index; }
        public bool active() { return flags().active(); }
        public void setActive(bool activate) { flags().setActive(activate); }

        public bool finalHarvest() { return flags().isFinalHarvest(); }
        public void setFinalHarvest(bool isfinal) { flags().setFinalHarvest(isfinal); }

        public bool scheduled() { return flags().isScheduled(); }
        public void setScheduled(bool issched) { flags().setIsScheduled(issched); }

        public bool enabled()
        {
            return flags().enabled();
        }

        public string name()
        {
            return mActivity != null ? mActivity.name() : "undefined";
        }

        public void setEnabled(bool do_enable)
        {
            flags().setEnabled(do_enable);
        }

        private ActivityFlags flags()
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
            return mEmptyFlags;
        }
    }
}
