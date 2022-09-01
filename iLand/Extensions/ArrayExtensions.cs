using System;
using System.Linq;

namespace iLand.Extensions
{
    public static class ArrayExtensions
    {
        // reimplement Array.Resize() since properties can't be passed as ref parameters
        // Could work around this restriction by declaring fields for all arrays and wrapping them in properties but that is
        // substantially more code than this extension method.
        // For now, it's assumed arrays are short enough it's more efficient to go through memory allocation. This is likely
        // accpetable for most dynamic iLand allocations as iLand either
        //
        // - grows populations of arrays in size because it didn't know how long to make them initially, leaving little scope
        //   for reuse via ArrayPool<T> 
        // - only occasionally shortens tree lists based on mortality
        // - could reuse existing arrays when loading new chunks of weather data, if that was supported
        //
        // When reuse is possible, ArrayPool<T> tends to becomes more efficient than new somewhat above 1 kB, which is arrays
        // of ~300 Int32 or float values.
        public static T[] Resize<T>(this T[] array, int newSize)
        {
            if (newSize == 0)
            {
                return Array.Empty<T>();
            }

            T[] resizedArray = new T[newSize];

            int maxLength = Math.Min(array.Length, newSize);
            Array.Copy(array, resizedArray, maxLength);
            return resizedArray;
        }

        public static Span<T> Slice<T>(this T[] array, int start)
        {
            return array.AsSpan().Slice(start);
        }

        public static Span<T> Slice<T>(this T[] array, int start, int length)
        {
            return array.AsSpan().Slice(start, length);
        }

        public static float Sum(this float[] array)
        {
            float sum = 0.0F;
            for (int index = 0; index < array.Length; ++index)
            {
                sum += array[index];
            }
            return sum;
        }

        public static void ToMonthlyAverages(this float[] dailyAverages, Span<float> monthlyAverages)
        {
            if ((dailyAverages.Length < Constant.Time.DaysInYear) || (dailyAverages.Length > Constant.Time.DaysInLeapYear))
            {
                throw new ArgumentOutOfRangeException(nameof(dailyAverages));
            }
            if (monthlyAverages.Length != Constant.Time.MonthsInYear)
            {
                throw new ArgumentOutOfRangeException(nameof(monthlyAverages));
            }

            monthlyAverages[0] = dailyAverages[0..31].Average();
        }
    }
}
