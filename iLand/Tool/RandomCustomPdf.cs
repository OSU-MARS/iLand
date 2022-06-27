using System;
using System.Diagnostics;

namespace iLand.Tool
{
    /** @class RandomCustomPDF provide random numbers with a user defined probaility density function.
        Call setup() or use the constructor to provide a function-expression with one variable (e.g. 'x^2').
        Call get() to retrieve a random number that follows the given probabilty density function. The provided function
        is not bound to a specific value range, but should produce values below 40.
        */
    internal class RandomCustomPdf
    {
        private readonly RandomWeighted mRandomIndex;
        private Expression? mExpression;
        private int mSteps;
        private float mLowerBound, mUpperBound;
        private float mDeltaX;
        private bool mSumFunction;

        public string? ProbabilityDensityFunction { get; private set; }

        public RandomCustomPdf()
        {
            this.mExpression = null;
            this.mRandomIndex = new RandomWeighted();
        }

        public RandomCustomPdf(string probabilityDensityExpression)
            : this()
        {
            this.Setup(probabilityDensityExpression);
        }

        /** setup of the properites of the RandomCustomPDF.
            @p funcExpr the probability density function is given as a string for an Expression. The variable (e.g. 'x')
            @p lowerBound lowest possible value of the random numbers (default=0)
            @p upperBound highest possible value of the random numbers (default=1)
            @p isSumFunc if true, the function given in 'funcExpr' is a cumulative probabilty density function (default=false)
            @p stepCount internal degree of 'slots' - the more slots, the more accurate (default=100)
         */
        public void Setup(string probabilityDensityExpression, float lowerBound = 0, float upperBound = 1, bool isSumFunc = false, int stepCount = 100)
        {
            this.ProbabilityDensityFunction = probabilityDensityExpression;
            this.mSteps = stepCount;
            this.mSumFunction = isSumFunc;
            this.mExpression = new(probabilityDensityExpression);

            this.mRandomIndex.Setup(mSteps);
            this.mLowerBound = lowerBound;
            this.mUpperBound = upperBound;
            this.mDeltaX = (mUpperBound - mLowerBound) / mSteps;
            float stepWidth = 1.0F / mSteps;
            for (int step = 0; step < mSteps; ++step)
            {
                float x1 = this.mLowerBound + step * this.mDeltaX;
                float x2 = x1 + this.mDeltaX;
                // p1, p2: werte der pdf bei unterer und oberer grenze des aktuellen schrittes
                float p1 = (float)this.mExpression.Evaluate(x1);
                float p2 = (float)this.mExpression.Evaluate(x2);
                // areaval: numerische integration zwischen x1 und x2
                float stepProbability = 0.5F * (p1 + p2) * stepWidth;
                if (isSumFunc)
                {
                    stepProbability -= p1 * stepWidth; // summenwahrscheinlichkeit: nur das Delta zaehlt.
                                                // set weighted operiert mit integers . umrechnung: * huge_val
                }
                this.mRandomIndex.SetCellWeight(step, (int)(stepProbability * 100000000));
            }
        }

        public float GetRandomValue(RandomGenerator randomGenerator)
        {
            // zufallszahl ziehen.
            if (this.mExpression == null)
            {
                throw new NotSupportedException("GetRandomValue() called before Setup()."); // not set up properly                                                             
            }

            // (1) select slot randomly:
            int slot = mRandomIndex.GetRandomCellIndex(randomGenerator);
            // the current slot is:
            float basevalue = mLowerBound + slot * mDeltaX;
            // (2): draw a uniform random number within the slot
            float value = randomGenerator.GetRandomFloat(basevalue, basevalue + mDeltaX);
            return value;
        }

        public float GetProbabilityInRange(float lowerBound, float upperBound)
        {
            if (this.mSumFunction)
            {
                if (this.mExpression == null)
                {
                    throw new NotSupportedException("GetProbabilityOfRange() called before Setup()."); // not set up properly                                                             
                }
                double p1 = this.mExpression.Evaluate(lowerBound);
                double p2 = this.mExpression.Evaluate(upperBound);
                double probabilityInRange = p2 - p1;
                Debug.Assert(probabilityInRange >= 0.0);
                return (float)probabilityInRange;
            }

            // Wahrscheinlichkeit, dass wert zwischen lower- und upper-bound liegt.
            if (lowerBound > upperBound)
            {
                return 0.0F;
            }
            if (lowerBound < mLowerBound || upperBound > mUpperBound)
            {
                return 0.0F;
            }
            // "steps" is the resolution between lower and upper bound
            int iLow = (int)((mUpperBound - mLowerBound) / (double)mSteps * (lowerBound - mLowerBound));
            int iHigh = (int)((mUpperBound - mLowerBound) / (double)mSteps * (upperBound - mUpperBound));
            if (iLow < 0 || iLow >= mSteps || iHigh < 0 || iHigh >= mSteps)
            {
                return -1;
            }
            return mRandomIndex.GetRelativeWeight(iLow, iHigh);
        }
    }
}
