using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.tools
{
    internal class UpdateState
    {
        private int mCurrentVal; // state of last explicit "update"
        private int mVal; // current state
        private List<UpdateState> mChilds;
        private Dictionary<UpdateState, int> mSavedStates;
        private int value() { return mVal; } // return current value

        public UpdateState()
        {
            mChilds = new List<UpdateState>();
            mCurrentVal = 0;
            mSavedStates = new Dictionary<UpdateState, int>();
            mVal = 0;
        }

        public void addChild(UpdateState state) { mChilds.Add(state); }

        public void invalidate(bool self)
        {
            if (self)
            {
                mVal++;
            }
            foreach (UpdateState s in mChilds)
            {
                s.invalidate(true);
            }
        }

        public void saveState(UpdateState state)
        {
            mSavedStates[state] = state.mVal;
        }

        public bool hasChanged(UpdateState state)
        {
            if (!mSavedStates.ContainsKey(state))
            {
                return true;
            }
            Debug.WriteLine("hasChanged: saved: " + mSavedStates[state] + " current: " + state.mVal);
            return mSavedStates[state] != state.mVal;
        }

        // set internal state to the current state
        public void update()
        {
            mCurrentVal = mVal;
        }

        // check if state needs update
        public bool needsUpdate()
        {
            return mVal > mCurrentVal;
        }
    }
}
