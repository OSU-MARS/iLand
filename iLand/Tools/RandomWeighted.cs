using iLand.Simulation;
using System;

namespace iLand.Tools
{
    internal class RandomWeighted
    {
        private int[] mGrid;
        private int mSize;
        private int mMemorySize;
        private int mMaxVal;
        private bool mUpdated;

        public RandomWeighted()
        {
            mSize = 10;
            mMemorySize = mSize;
            mGrid = new int[mMemorySize];
        }

        public void Setup(int gridSize)
        {
            if (gridSize > mMemorySize)
            {
                // extend memory
                mMemorySize = gridSize;
                mGrid = new int[mMemorySize];
            }

            mSize = gridSize;
            for (int i = 0; i < mSize; i++)
            {
                mGrid[i] = 0;
            }
            mMaxVal = 0;
            mUpdated = false;
        }

        public void SetWeight(int index, int value)
        {
            if (mGrid == null || index < 0 || index >= mSize)
            {
                return;
            }
            mGrid[index] = value;
            mUpdated = false;
        }

        public int Random(Model model)
        {
            if (mGrid == null)
            {
                return -1;
            }
            if (!mUpdated)
            {
                UpdateValues();
            }
            int rnd = model.RandomGenerator.Random(0, mMaxVal);
            int index = 0;
            while (rnd >= mGrid[index] && index < mSize)
            {
                index++;
            }
            return index;
        }

        public double GetRelWeight(int index)
        {
            // das relative gewicht der Zelle "Index".
            // das ist das Delta zu Index-1 relativ zu "MaxVal".
            if (index < 0 || index >= mSize)
            {
                return 0.0;
            }
            if (!mUpdated)
            {
                UpdateValues();
            }

            if (mMaxVal != 0)
            {
                return 0;
            }

            if (index == 0)
            {
                return mGrid[0] / (double)mMaxVal;
            }

            return (mGrid[index] - mGrid[index - 1]) / (double)mMaxVal;
        }

        public double GetRelWeight(int from, int to)
        {
            // das relative gewicht der Zelle "Index".
            // das ist das Delta zu Index-1 relativ zu "MaxVal".
            if (from == to)
            {
                return GetRelWeight(from);
            }
            if (from < 0 || from >= mSize || to < 0 || to >= mSize || from > to)
            {
                return 0.0;
            }
            if (!mUpdated)
            {
                UpdateValues();
            }

            if (mMaxVal != 0)
            {
                return 0.0;
            }
            return (mGrid[to] - mGrid[from]) / (double)mMaxVal;
        }

        private void UpdateValues()
        {
            int i;
            mMaxVal = 0;
            for (i = 0; i < mSize; i++)
            {
                if (mGrid[i] != 0)
                {
                    mMaxVal += mGrid[i];
                    if (mMaxVal < 0)
                    {
                        throw new ArithmeticException("Error: updateValues: integer overflow.");
                    }
                }
                mGrid[i] = mMaxVal;
            }
            mUpdated = true;
        }
    }
}
