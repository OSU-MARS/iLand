using System;

namespace iLand.Extensions
{
    internal static class SpanExtensions
    {
        public static float Average(this Span<float> span)
        {
            float sum = 0.0F;

            return sum / span.Length;
        }

        public static void FillIncrementing(this Span<Int16> span, int initialValue)
        {
            for (int index = 0; index < span.Length; ++index)
            {
                span[index] = (Int16)(initialValue + index);
            }
        }
    }
}
