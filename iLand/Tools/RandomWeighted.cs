using System;

namespace iLand.Tools
{
    internal class RandomWeighted
    {
        private int[] mCellProbabilities;
        private int mSize;
        private int mMaxVal;
        private bool mUpdated;

        public RandomWeighted()
        {
            this.mSize = 10;
            this.mCellProbabilities = new int[this.mSize];
        }

        public void Setup(int gridCellCount)
        {
            if (gridCellCount > this.mCellProbabilities.Length)
            {
                // extend memory if needed
                this.mCellProbabilities = new int[gridCellCount];
            }

            this.mSize = gridCellCount;
            for (int index = 0; index < this.mSize; ++index)
            {
                this.mCellProbabilities[index] = 0;
            }
            this.mMaxVal = 0;
            this.mUpdated = false;
        }

        public void SetCellWeight(int index, int value)
        {
            this.mCellProbabilities[index] = value;
            this.mUpdated = false;
        }

        public int GetRandomCellIndex(RandomGenerator randomGenerator)
        {
            if (this.mCellProbabilities == null)
            {
                return -1;
            }
            if (this.mUpdated == false)
            {
                this.UpdateValues();
            }

            int random = randomGenerator.GetRandomInteger(0, this.mMaxVal);
            int index = 0;
            while (random >= this.mCellProbabilities[index] && index < this.mSize)
            {
                ++index;
            }
            return index;
        }

        public float GetRelativeWeight(int index)
        {
            // das relative gewicht der Zelle "Index".
            // das ist das Delta zu Index-1 relativ zu "MaxVal".
            if (index < 0 || index >= this.mSize)
            {
                return 0.0F;
            }
            if (this.mUpdated == false)
            {
                this.UpdateValues();
            }

            if (mMaxVal != 0)
            {
                return 0.0F;
            }

            if (index == 0)
            {
                return this.mCellProbabilities[0] / (float)this.mMaxVal;
            }

            return (this.mCellProbabilities[index] - this.mCellProbabilities[index - 1]) / (float)this.mMaxVal;
        }

        public float GetRelativeWeight(int from, int to)
        {
            // das relative gewicht der Zelle "Index".
            // das ist das Delta zu Index-1 relativ zu "MaxVal".
            if (from == to)
            {
                return this.GetRelativeWeight(from);
            }
            if (from < 0 || from >= this.mSize || to < 0 || to >= this.mSize || from > to)
            {
                return 0.0F;
            }
            if (this.mUpdated == false)
            {
                this.UpdateValues();
            }

            if (mMaxVal != 0)
            {
                return 0.0F;
            }
            return (this.mCellProbabilities[to] - this.mCellProbabilities[from]) / (float)this.mMaxVal;
        }

        private void UpdateValues()
        {
            this.mMaxVal = 0;
            for (int index = 0; index < this.mSize; ++index)
            {
                if (this.mCellProbabilities[index] != 0)
                {
                    this.mMaxVal += this.mCellProbabilities[index];
                    if (this.mMaxVal < 0)
                    {
                        throw new ArithmeticException("Error: updateValues: integer overflow.");
                    }
                }
                this.mCellProbabilities[index] = this.mMaxVal;
            }
            this.mUpdated = true;
        }
    }
}
