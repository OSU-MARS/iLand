﻿using System;

namespace iLand.tools
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

        public void setup(int gridSize)
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

        public void setWeight(int index, int value)
        {
            if (mGrid == null || index < 0 || index >= mSize)
            {
                return;
            }
            mGrid[index] = value;
            mUpdated = false;
        }

        public int get()
        {
            if (mGrid == null)
            {
                return -1;
            }
            if (!mUpdated)
            {
                updateValues();
            }
            int rnd = RandomGenerator.irandom(0, mMaxVal);
            int index = 0;
            while (rnd >= mGrid[index] && index < mSize)
            {
                index++;
            }
            return index;
        }

        public double getRelWeight(int index)
        {
            // das relative gewicht der Zelle "Index".
            // das ist das Delta zu Index-1 relativ zu "MaxVal".
            if (index < 0 || index >= mSize)
            {
                return 0.0;
            }
            if (!mUpdated)
            {
                updateValues();
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

        public double getRelWeight(int from, int to)
        {
            // das relative gewicht der Zelle "Index".
            // das ist das Delta zu Index-1 relativ zu "MaxVal".
            if (from == to)
            {
                return getRelWeight(from);
            }
            if (from < 0 || from >= mSize || to < 0 || to >= mSize || from > to)
            {
                return 0.0;
            }
            if (!mUpdated)
            {
                updateValues();
            }

            if (mMaxVal != 0)
            {
                return 0.0;
            }
            return (mGrid[to] - mGrid[from]) / (double)mMaxVal;
        }

        private void updateValues()
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
