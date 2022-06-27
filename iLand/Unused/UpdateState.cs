using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.Tool
{
    internal class UpdateState
    {
        private int mCurrentVal; // state of last explicit "update"
        private int mVal; // current state
        private readonly List<UpdateState> mChildren;
        private readonly Dictionary<UpdateState, int> mSavedStates;

        public UpdateState()
        {
            mChildren = new List<UpdateState>();
            mCurrentVal = 0;
            mSavedStates = new Dictionary<UpdateState, int>();
            mVal = 0;
        }

        public void AddChild(UpdateState state) 
        {
            mChildren.Add(state); 
        }

        public void Invalidate(bool self)
        {
            if (self)
            {
                mVal++;
            }
            foreach (UpdateState s in mChildren)
            {
                s.Invalidate(true);
            }
        }

        public void SaveState(UpdateState state)
        {
            mSavedStates[state] = state.mVal;
        }

        public bool HasChanged(UpdateState state)
        {
            if (!mSavedStates.ContainsKey(state))
            {
                return true;
            }
            Debug.WriteLine("hasChanged: saved: " + mSavedStates[state] + " current: " + state.mVal);
            return mSavedStates[state] != state.mVal;
        }

        // set internal state to the current state
        public void Update()
        {
            mCurrentVal = mVal;
        }

        // check if state needs update
        public bool NeedsUpdate()
        {
            return mVal > mCurrentVal;
        }
    }
}
