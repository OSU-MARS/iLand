namespace iLand.tools
{
    /** @class RandomIndex Access each index of a given size in a random order.
        Example-Usage:
        @code
        RandomIndex r(100); // create
        while (r.next())
         qDebug() << r.index(); // prints out 100 numbers (0..99) in a random order.
        @endcode
        */
    internal class RandomIndex
    {
        private int mCount;
        private int mIndex; ///< currently selected
        private char[] mField;
        private int mRemaining;

        public int index() { return mIndex; } ///< retrieve (random) index

        public RandomIndex(int aCount)
        {
            mField = null;
            mCount = aCount;
            if (mCount > 0)
            {
                mField = new char[mCount];
                for (int i = 0; i < mCount; i++)
                {
                    mField[i] = 'a';
                }
            }
            mIndex = -1;
            mRemaining = mCount;
        }

        public bool next()
        {
            if (mRemaining == 0)
            {
                mIndex = -1;
                return false;
            }
            mRemaining--;
            int random_index = RandomGenerator.irandom(0, mRemaining + 1); //RandomRange(0,mRemaining+1);
            int found = 0;
            for (int i = 0; i < mCount; i++)
            {
                if (mField[i] == 'a')
                {
                    if (random_index == found)
                    {
                        mIndex = i;
                        mField[i] = 'b';
                        return true;
                    }
                    found++;
                }
            }
            return false;
        }
    }
}
