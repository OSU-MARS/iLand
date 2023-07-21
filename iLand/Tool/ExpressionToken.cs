namespace iLand.Tool
{
    internal struct ExpressionToken
    {
        public ExpressionTokenType Type { get; set; }
        public float Value { get; set; }
        public int Index { get; set; }

        public readonly override string? ToString()
        {
            return this.Type switch
            {
                ExpressionTokenType.Function => Expression.MathFunctions[this.Index],
                ExpressionTokenType.Number => this.Value.ToString(),
                ExpressionTokenType.Operator => new string((char)this.Index, 1),
                ExpressionTokenType.Stop => "<stop>",
                ExpressionTokenType.Unknown => "<unknown>",
                // Compare, Delimiter, Logical, Variable
                _ => this.Type.ToString().ToLowerInvariant() + "(" + this.Index + ")"
            };
        }
    }
}
