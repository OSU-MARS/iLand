using System;

namespace iLand.tools
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
            Setup(densityFunction);
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
            double x1, x2;
            double p1, p2;
            double areaval;
            double step_width = 1.0 / mSteps;
            for (int i = 0; i < mSteps; i++)
            {
                x1 = mLowerBound + i * mDeltaX;
                x2 = x1 + mDeltaX;
                // p1, p2: werte der pdf bei unterer und oberer grenze des aktuellen schrittes
                p1 = mExpression.Calculate(x1);
                p2 = mExpression.Calculate(x2);
                // areaval: numerische integration zwischen x1 und x2
                areaval = (p1 + p2) / 2 * step_width;
                if (isSumFunc)
                {
                    areaval -= p1 * step_width; // summenwahrscheinlichkeit: nur das Delta zaehlt.
                                                // tsetWeightghted operiert mit integers . umrechnung: * huge_val
                }
                mRandomIndex.SetWeight(i, (int)(areaval * 100000000));
            }
        }

        public double Get()
        {
            // zufallszahl ziehen.
            if (mExpression == null)
            {
                throw new NotSupportedException("TRandomCustomPDF: get() without setup()!"); // not set up properly                                                             
            }

            // (1) select slot randomly:
            int slot = mRandomIndex.Random();
            // the current slot is:
            double basevalue = mLowerBound + slot * mDeltaX;
            // (2): draw a uniform random number within the slot
            double value = RandomGenerator.Random(basevalue, basevalue + mDeltaX);
            return value;
        }

        public double GetProbOfRange(double lowerBound, double upperBound)
        {
            if (mSumFunction)
            {
                double p1, p2;
                p1 = mExpression.Calculate(lowerBound);
                p2 = mExpression.Calculate(upperBound);
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
            return mRandomIndex.GetRelWeight(iLow, iHigh);
        }
    }
}
