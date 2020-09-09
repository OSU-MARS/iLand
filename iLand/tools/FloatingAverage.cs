using System.Collections.Generic;

namespace iLand.tools
{
    // BUGBUG: unused, can be removed
    internal class FloatingAverage
    {
        private double mCurrentAverage;
        private List<double> mData;
        private int mSize;
        private int mPos;
        private bool mFilled;
        private double mInitValue;

        public double average() { return mCurrentAverage; } ///< retrieve current average

        public FloatingAverage()
        {
            mCurrentAverage = 0;
            mData = new List<double>();
            mSize = 0;
            mInitValue = 0.0;
            mPos = -1;
        }

        public FloatingAverage(int size)
        : this()
        {
            setup(size);
        }

        public void setup(int size, double InitValue = 0.0)
        {
            mInitValue = InitValue;
            mSize = size;
            mData.Capacity = mSize;
            mPos = -1;
            mCurrentAverage = 0;
            mFilled = false;
            for (int i = 0; i < size; i++)
            {
                mData[i] = mInitValue;
            }
        }

        public double add(double add_value)
        {
            mPos++;
            if (mPos >= mSize)
            {
                mPos = 0;      // rollover again
                mFilled = true;
            }
            mData[mPos] = add_value;

            int countto = mSize;
            if (!mFilled)
            {
                countto = mPos + 1;
            }
            double sum = 0;
            for (int i = 0; i < countto; i++)
            {
                sum += mData[i];
            }
            if (countto != 0)
            {
                mCurrentAverage = sum / countto;
            }
            else
            {
                mCurrentAverage = mInitValue; // kann sein, wenn als erster wert 0 übergeben wird.
            }
            return mCurrentAverage;
        }

        public double sum()
        {
            if (mFilled)
            {
                return mCurrentAverage * mSize;
            }
            else
            {
                return mCurrentAverage * (mPos + 1);
            }
        }
    }
}
