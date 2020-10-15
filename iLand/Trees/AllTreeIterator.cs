using iLand.Simulation;
using iLand.World;

namespace iLand.Trees
{
    internal class AllTreeIterator
    {
        private readonly Model mModel;
        private int mCurrent;
        private int mRUIterator;

        public AllTreeIterator(Model model)
        {
            mModel = model;
            this.Reset();
        }

        public Tree Current() { return mCurrent > 0 ? this.CurrentRU().Trees[mCurrent] : null; }
        public ResourceUnit CurrentRU() { return mModel.ResourceUnits[mRUIterator]; }
        // public Tree operator *() { return current(); }

        public void Reset() 
        {
            mRUIterator = 0;
            mCurrent = -1;
        }

        /** iterate over all trees of the model. return NULL if all trees processed.
          Usage:
          @code
          AllTreeIterator trees(model);
          while (Tree *tree = trees.next()) { // returns NULL when finished.
             tree->something(); // do something
          }
          @endcode  */
        public Tree MoveNext()
        {
            if (mCurrent == -1)
            {
                for (; mRUIterator < mModel.ResourceUnits.Count; ++ mRUIterator)
                {
                    // move to first RU with trees
                    if (this.CurrentRU().Trees.Count > 0)
                    {
                        break;
                    }
                }
                // finished if all RU processed
                if (mRUIterator == mModel.ResourceUnits.Count)
                {
                    return null;
                }
                mCurrent = 0;
            }
            if (mCurrent == this.CurrentRU().Trees.Count)
            {
                // move to next RU with trees
                for (++mRUIterator; mRUIterator < mModel.ResourceUnits.Count; ++mRUIterator)
                {
                    if (this.CurrentRU().Trees.Count > 0)
                    {
                        break;
                    }
                }
                if (mRUIterator == mModel.ResourceUnits.Count)
                {
                    mCurrent = -1;
                    return null; // finished!!
                }
                else
                {
                    mCurrent = 0;
                }
            }

            return this.CurrentRU().Trees[mCurrent++];
        }

        public Tree MoveNextLiving()
        {
            for (Tree t = MoveNext(); t != null; t = MoveNext())
            {
                if (!t.IsDead())
                {
                    return t;
                }
            }
            return null;
        }
    }
}
