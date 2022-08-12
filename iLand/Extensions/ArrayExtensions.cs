﻿using System;

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
            T[] resizedArray = new T[newSize];

            int maxLength = Math.Min(array.Length, newSize);
            Array.Copy(array, resizedArray, maxLength);
            return resizedArray;
        }
    }
}
