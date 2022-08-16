using System;

namespace iLand.Extensions
{
    internal static class SpanExtensions
    {
        public static void FillIncrementing(this Span<Int16> span, int initialValue)
        {
            for (int index = 0; index < span.Length; ++index)
            {
                span[index] = (Int16)(initialValue + index);
            }
        }
    }
}
