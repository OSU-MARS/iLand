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
        private readonly RandomWeighted randomIndex;
        private Expression? expression;
        private int steps;
        private float lowerBound, upperBound;
        private float deltaX;
        private bool sumFunction;

        public string? ProbabilityDensityFunction { get; private set; }

        public RandomCustomPdf()
        {
            this.expression = null;
            this.randomIndex = new();
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
            this.steps = stepCount;
            this.sumFunction = isSumFunc;
            this.expression = new(probabilityDensityExpression);

            this.randomIndex.Setup(steps);
            this.lowerBound = lowerBound;
            this.upperBound = upperBound;
            this.deltaX = (this.upperBound - this.lowerBound) / this.steps;
            float stepWidth = 1.0F / steps;
            for (int step = 0; step < steps; ++step)
            {
                float x1 = this.lowerBound + step * this.deltaX;
                float x2 = x1 + this.deltaX;
                // p1, p2: werte der pdf bei unterer und oberer grenze des aktuellen schrittes
                float p1 = this.expression.Evaluate(x1);
                float p2 = this.expression.Evaluate(x2);
                // areaval: numerische integration zwischen x1 und x2
                float stepProbability = 0.5F * (p1 + p2) * stepWidth;
                if (isSumFunc)
                {
                    stepProbability -= p1 * stepWidth; // summenwahrscheinlichkeit: nur das Delta zaehlt.
                                                // set weighted operiert mit integers . umrechnung: * huge_val
                }
                this.randomIndex.SetCellWeight(step, (int)(stepProbability * 100000000));
            }
        }

        public float GetRandomValue(RandomGenerator randomGenerator)
        {
            // zufallszahl ziehen.
            if (this.expression == null)
            {
                throw new NotSupportedException("GetRandomValue() called before Setup()."); // not set up properly                                                             
            }

            // (1) select slot randomly:
            int slot = randomIndex.GetRandomCellIndex(randomGenerator);
            // the current slot is:
            float basevalue = lowerBound + slot * deltaX;
            // (2): draw a uniform random number within the slot
            float value = randomGenerator.GetRandomFloat(basevalue, basevalue + deltaX);
            return value;
        }

        public float GetProbabilityInRange(float lowerBound, float upperBound)
        {
            if (this.sumFunction)
            {
                if (this.expression == null)
                {
                    throw new NotSupportedException("GetProbabilityOfRange() called before Setup()."); // not set up properly                                                             
                }
                float p1 = this.expression.Evaluate(lowerBound);
                float p2 = this.expression.Evaluate(upperBound);
                float probabilityInRange = p2 - p1;
                Debug.Assert(probabilityInRange >= 0.0);
                return probabilityInRange;
            }

            // Wahrscheinlichkeit, dass wert zwischen lower- und upper-bound liegt.
            if (lowerBound > upperBound)
            {
                return 0.0F;
            }
            if ((lowerBound < this.lowerBound) || (upperBound > this.upperBound))
            {
                return 0.0F;
            }
            // "steps" is the resolution between lower and upper bound
            int iLow = (int)((this.upperBound - this.lowerBound) / (float)this.steps * (lowerBound - this.lowerBound));
            int iHigh = (int)((this.upperBound - this.lowerBound) / (float)this.steps * (upperBound - this.upperBound));
            if (iLow < 0 || iLow >= steps || iHigh < 0 || iHigh >= steps)
            {
                return -1;
            }
            return randomIndex.GetRelativeWeight(iLow, iHigh);
        }
    }
}
