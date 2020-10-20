using iLand.Simulation;
using iLand.World;

namespace iLand.Trees
{
    internal class AllTreeEnumerator
    {
        private readonly Model mModel;
        private int mRUindex;
        private int mTreeIndex;

        public AllTreeEnumerator(Model model)
        {
            mModel = model;
            this.Reset();
        }

        public Tree Current() { return mTreeIndex > 0 ? this.CurrentRU().Trees[mTreeIndex] : null; }
        public ResourceUnit CurrentRU() { return mModel.ResourceUnits[mRUindex]; }
        // public Tree operator *() { return current(); }

        public void Reset() 
        {
            mRUindex = 0;
            mTreeIndex = -1;
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
            if (mTreeIndex == -1)
            {
                for (; mRUindex < mModel.ResourceUnits.Count; ++ mRUindex)
                {
                    // move to first RU with trees
                    if (this.CurrentRU().Trees.Count > 0)
                    {
                        break;
                    }
                }
                // finished if all RU processed
                if (mRUindex == mModel.ResourceUnits.Count)
                {
                    return null;
                }
                mTreeIndex = 0;
            }
            if (mTreeIndex == this.CurrentRU().Trees.Count)
            {
                // move to next RU with trees
                for (++mRUindex; mRUindex < mModel.ResourceUnits.Count; ++mRUindex)
                {
                    if (this.CurrentRU().Trees.Count > 0)
                    {
                        break;
                    }
                }
                if (mRUindex == mModel.ResourceUnits.Count)
                {
                    mTreeIndex = -1;
                    return null; // finished!!
                }
                else
                {
                    mTreeIndex = 0;
                }
            }

            return this.CurrentRU().Trees[mTreeIndex++];
        }

        public Tree MoveNextLiving()
        {
            for (Tree tree = this.MoveNext(); tree != null; tree = this.MoveNext())
            {
                if (tree.IsDead() == false)
                {
                    return tree;
                }
            }
            return null;
        }
    }
}
