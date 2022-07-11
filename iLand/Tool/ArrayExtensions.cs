using System;

namespace iLand.Tool
{
    public static class ArrayExtensions
    {
        // reimplement Array.Resize() since properties can't be passed as ref parameters
        // Could work around this restriction by declaring fields for all arrays and wrapping them in properties
        // but that is substantially more code than this extension method.
        public static T[] Resize<T>(this T[] array, int newSize)
        {
            T[] resizedArray = new T[newSize];

            int maxLength = Math.Min(array.Length, newSize);
            Array.Copy(array, resizedArray, maxLength);
            return resizedArray;
        }
    }
}
