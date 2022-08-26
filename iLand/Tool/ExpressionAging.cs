using System;

namespace iLand.Tool
{
    public class ExpressionAging
    {
        private const string StartFormat = "1/(1 + (x/";

        private float harmonicExponent;
        private float harmonicMultiplier;

        public float Evaluate(float ageHeightHarmonicMean)
        {
            return 1.0F / (1.0F + MathF.Pow(this.harmonicMultiplier * ageHeightHarmonicMean, this.harmonicExponent));
        }

        public void Parse(string expression)
        {
            // supported form: 1/(1 + (x/a0)^b0)
            if ((expression.StartsWith(ExpressionAging.StartFormat, StringComparison.Ordinal) == false) || (expression[^1] != ')'))
            {
                throw ExpressionAging.ParseError(expression);
            }

            int divisorStart = ExpressionAging.StartFormat.Length;
            int divisorEnd = expression[divisorStart..^1].IndexOf(')') + divisorStart;
            int exponentStart = expression[divisorStart..^1].LastIndexOf('^') + divisorStart + 1;

            if ((divisorEnd < divisorStart) || (exponentStart <= divisorStart))
            {
                throw ExpressionAging.ParseError(expression);
            }

            this.harmonicMultiplier = 1.0F / Single.Parse(expression[divisorStart..divisorEnd]);
            this.harmonicExponent = Single.Parse(expression[exponentStart..^1]);
        }

        private static Exception ParseError(string expression)
        {
            return new ArgumentOutOfRangeException(nameof(expression), "Expression '" + expression + "' doesn't match the format expected for an aging expression.");
        }
    }
}
