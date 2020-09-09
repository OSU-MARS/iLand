namespace iLand.core
{
    internal class AllTreeIterator
    {
        private readonly Model mModel;
        private int mCurrent;
        private int mRUIterator;

        public AllTreeIterator(Model model)
        {
            mModel = model;
            this.reset();
        }

        public Tree current() { return mCurrent > 0 ? this.currentRU().trees()[mCurrent] : null; }
        public ResourceUnit currentRU() { return mModel.ruList()[mRUIterator]; }
        // public Tree operator *() { return current(); }

        public void reset() 
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
        public Tree next()
        {
            if (mCurrent == -1)
            {
                for (; mRUIterator < mModel.ruList().Count; ++ mRUIterator)
                {
                    // move to first RU with trees
                    if (this.currentRU().trees().Count > 0)
                    {
                        break;
                    }
                }
                // finished if all RU processed
                if (mRUIterator == mModel.ruList().Count)
                {
                    return null;
                }
                mCurrent = 0;
            }
            if (mCurrent == this.currentRU().trees().Count)
            {
                // move to next RU with trees
                for (++mRUIterator; mRUIterator < mModel.ruList().Count; ++mRUIterator)
                {
                    if (this.currentRU().trees().Count > 0)
                    {
                        break;
                    }
                }
                if (mRUIterator == mModel.ruList().Count)
                {
                    mCurrent = -1;
                    return null; // finished!!
                }
                else
                {
                    mCurrent = 0;
                }
            }

            return this.currentRU().trees()[mCurrent++];
        }

        public Tree nextLiving()
        {
            for (Tree t = next(); t != null; t = next())
            {
                if (!t.isDead())
                {
                    return t;
                }
            }
            return null;
        }
    }
}
