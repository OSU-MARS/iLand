using System;

namespace iLand.Tools
{
    /** @class RandomCustomPDF provide random numbers with a user defined probaility density function.
        Call setup() or use the constructor to provide a function-expression with one variable (e.g. 'x^2').
        Call get() to retrieve a random number that follows the given probabilty density function. The provided function
        is not bound to a specific value range, but should produce values below 40.
        */
    internal class RandomCustomPdf
    {
        private readonly RandomWeighted mRandomIndex;
        private Expression mExpression;
        private int mSteps;
        private double mLowerBound, mUpperBound;
        private double mDeltaX;
        private bool mSumFunction;

        public string DensityFunction { get; private set; }

        public RandomCustomPdf()
        {
            this.mExpression = null;
            this.mRandomIndex = new RandomWeighted();
        }

        public RandomCustomPdf(string densityFunction)
            : this()
        {
            this.Setup(densityFunction);
        }

        /** setup of the properites of the RandomCustomPDF.
            @p funcExpr the probability density function is given as a string for an Expression. The variable (e.g. 'x')
            @p lowerBound lowest possible value of the random numbers (default=0)
            @p upperBound highest possible value of the random numbers (default=1)
            @p isSumFunc if true, the function given in 'funcExpr' is a cumulative probabilty density function (default=false)
            @p stepCount internal degree of 'slots' - the more slots, the more accurate (default=100)
         */
        public void Setup(string funcExpr, double lowerBound = 0, double upperBound = 1, bool isSumFunc = false, int stepCount = 100)
        {
            DensityFunction = funcExpr;
            mSteps = stepCount;
            mSumFunction = isSumFunc;
            mExpression = new Expression(funcExpr);

            mRandomIndex.Setup(mSteps);
            mLowerBound = lowerBound;
            mUpperBound = upperBound;
            mDeltaX = (mUpperBound - mLowerBound) / mSteps;
            double step_width = 1.0 / mSteps;
            for (int step = 0; step < mSteps; ++step)
            {
                double x1 = mLowerBound + step * mDeltaX;
                double x2 = x1 + mDeltaX;
                // p1, p2: werte der pdf bei unterer und oberer grenze des aktuellen schrittes
                double p1 = mExpression.Evaluate(x1);
                double p2 = mExpression.Evaluate(x2);
                // areaval: numerische integration zwischen x1 und x2
                double areaval = (p1 + p2) / 2 * step_width;
                if (isSumFunc)
                {
                    areaval -= p1 * step_width; // summenwahrscheinlichkeit: nur das Delta zaehlt.
                                                // tsetWeightghted operiert mit integers . umrechnung: * huge_val
                }
                mRandomIndex.SetCellWeight(step, (int)(areaval * 100000000));
            }
        }

        public double GetRandomValue(RandomGenerator randomGenerator)
        {
            // zufallszahl ziehen.
            if (mExpression == null)
            {
                throw new NotSupportedException("TRandomCustomPDF: get() without setup()!"); // not set up properly                                                             
            }

            // (1) select slot randomly:
            int slot = mRandomIndex.GetRandomCellIndex(randomGenerator);
            // the current slot is:
            double basevalue = mLowerBound + slot * mDeltaX;
            // (2): draw a uniform random number within the slot
            double value = randomGenerator.GetRandomDouble(basevalue, basevalue + mDeltaX);
            return value;
        }

        public double GetProbabilityOfRange(double lowerBound, double upperBound)
        {
            if (mSumFunction)
            {
                double p1, p2;
                p1 = mExpression.Evaluate(lowerBound);
                p2 = mExpression.Evaluate(upperBound);
                return p2 - p1;
            }

            // Wahrscheinlichkeit, dass wert zwischen lower- und upper-bound liegt.
            if (lowerBound > upperBound)
            {
                return 0.0;
            }
            if (lowerBound < mLowerBound || upperBound > mUpperBound)
            {
                return 0.0;
            }
            // "steps" is the resolution between lower and upper bound
            int iLow, iHigh;
            iLow = (int)((mUpperBound - mLowerBound) / (double)mSteps * (lowerBound - mLowerBound));
            iHigh = (int)((mUpperBound - mLowerBound) / (double)mSteps * (upperBound - mUpperBound));
            if (iLow < 0 || iLow >= mSteps || iHigh < 0 || iHigh >= mSteps)
            {
                return -1;
            }
            return mRandomIndex.GetRelativeWeight(iLow, iHigh);
        }
    }
}
