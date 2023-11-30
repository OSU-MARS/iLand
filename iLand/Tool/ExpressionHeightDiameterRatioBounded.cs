using System;
using System.Globalization;

namespace iLand.Tool
{
    public class ExpressionHeightDiameterRatioBounded
    {
        private float dbhExponent;
        private float dbhMultiplier;
        private float upperBound;

        public float Evaluate(float dbhInCm)
        {
            float heightDiameterRatio = this.dbhMultiplier * MathF.Pow(dbhInCm, this.dbhExponent);
            if (heightDiameterRatio < this.upperBound)
            {
                return heightDiameterRatio;
            }

            return this.upperBound;
        }

        public void Parse(string expression)
        {
            // supported form: [min(] multiplier * d ^ exponent [, upperBound)]
            //   where multiplier is a0, a0*a1, a0*a1*a2, a0*a1*a2*..., a0*a1*(±a2±a3), ...
            //   where exponent is b0, b0*b1, b0*b1*b2, b0*b1*b2*...
            int diameterTermStart = 0;
            int diameterTermEnd = expression.Length;
            int upperBoundSplit = -1;
            if (expression.StartsWith("min(", StringComparison.Ordinal))
            {
                if (expression[^1] != ')')
                {
                    throw ExpressionHeightDiameterRatioBounded.ParseError(expression);
                }
                diameterTermStart = 4;
                upperBoundSplit = expression[diameterTermStart..^1].LastIndexOf(',') + diameterTermStart;
                if (upperBoundSplit == -1)
                {
                    throw ExpressionHeightDiameterRatioBounded.ParseError(expression);
                }
                diameterTermEnd = upperBoundSplit;
            }

            int exponentSplit = expression[diameterTermStart..diameterTermEnd].LastIndexOf('^') + diameterTermStart;
            int dIndex = expression[diameterTermStart..exponentSplit].LastIndexOf('d') + diameterTermStart;
            int multiplierSplit = expression[diameterTermStart..dIndex].LastIndexOf('*') + diameterTermStart;

            if ((exponentSplit < diameterTermStart) || (dIndex < diameterTermStart) || (multiplierSplit < diameterTermStart))
            {
                throw ExpressionHeightDiameterRatioBounded.ParseError(expression);
            }

            // parse diameter multiplier
            this.dbhMultiplier = 1.0F;
            int multiplierTermStart = diameterTermStart;
            do
            {
                int multiplierTermSplit = expression[multiplierTermStart..multiplierSplit].IndexOf('*');
                int multiplierTermEnd = multiplierTermSplit >= 0 ? multiplierTermStart + multiplierTermSplit : multiplierSplit;

                float dbhMultiplierTerm;
                int multiplierTermOpenParenthesis = expression[multiplierTermStart..multiplierTermEnd].IndexOf('(');
                if (multiplierTermOpenParenthesis != -1)
                {
                    multiplierTermStart += multiplierTermOpenParenthesis + 1;
                    int multiplierTermCloseParenthesis = expression[multiplierTermStart..multiplierTermEnd].IndexOf(')');
                    if (multiplierTermCloseParenthesis == -1)
                    {
                        throw ExpressionHeightDiameterRatioBounded.ParseError(expression);
                    }
                    multiplierTermEnd = multiplierTermStart + multiplierTermCloseParenthesis;

                    int multiplierTermOperator = expression[multiplierTermStart..multiplierTermEnd].LastIndexOf('-');
                    if (multiplierTermOperator <= 0) // term may be of the form (-a2 + a3), in which case the last index of - is the leading sign
                    {
                        multiplierTermOperator = expression[multiplierTermStart..multiplierTermEnd].LastIndexOf('+');
                        if (multiplierTermOperator == -1)
                        {
                            throw ExpressionHeightDiameterRatioBounded.ParseError(expression);
                        }
                    }
                    multiplierTermOperator += multiplierTermStart;

                    dbhMultiplierTerm = Single.Parse(expression[multiplierTermStart..multiplierTermOperator], NumberStyles.Float);
                    dbhMultiplierTerm += Single.Parse(expression[multiplierTermOperator..multiplierTermEnd], NumberStyles.Float);
                }
                else
                {
                    dbhMultiplierTerm = Single.Parse(expression[multiplierTermStart..multiplierTermEnd], NumberStyles.Float);
                }
                this.dbhMultiplier *= dbhMultiplierTerm;
                multiplierTermStart = multiplierTermEnd + 1;
            }
            while (multiplierTermStart < multiplierSplit);

            // parse diameter power
            int exponentStart = exponentSplit + 1;
            int exponentEnd = diameterTermEnd;
            int exponentOpenParenthesis = expression[exponentStart..exponentEnd].IndexOf('(');
            if (exponentOpenParenthesis != -1)
            {
                exponentStart += exponentOpenParenthesis + 1;
                int exponentCloseParenthesis = expression[exponentStart..exponentEnd].LastIndexOf(')');
                if (exponentCloseParenthesis == -1)
                {
                    throw ExpressionHeightDiameterRatioBounded.ParseError(expression);
                }

                exponentEnd = exponentStart + exponentCloseParenthesis;
            }

            this.dbhExponent = 1.0F;
            int exponentTermStart = exponentStart;
            do
            {
                int exponentTermSplit = expression[exponentTermStart..exponentEnd].IndexOf('*');
                int exponentTermEnd = exponentTermSplit >= 0 ? exponentTermStart + exponentTermSplit : exponentEnd;
                this.dbhExponent *= Single.Parse(expression[exponentTermStart..exponentTermEnd], NumberStyles.Float);
                exponentTermStart = exponentTermEnd + 1;
            }
            while (exponentTermStart < exponentEnd);

            if (upperBoundSplit == -1)
            {
                this.upperBound = Single.MaxValue;
            }
            else
            {
                this.upperBound = Single.Parse(expression[(upperBoundSplit + 1)..^1], NumberStyles.Float);
            }
        }

        private static ArgumentOutOfRangeException ParseError(string expression)
        {
            return new ArgumentOutOfRangeException(nameof(expression), "Expression '" + expression + "' doesn't match the format expected for a height:diameter ratio bound.");
        }
    }
}
