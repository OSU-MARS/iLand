using System;

namespace iLand.Extensions
{
    internal static class SpanExtensions
    {
        public static void FillIncrementing(this Span<int> span, int initialValue)
        {
            for (int index = 0; index < span.Length; ++index)
            {
                span[index] = initialValue + index;
            }
        }
    }
}
