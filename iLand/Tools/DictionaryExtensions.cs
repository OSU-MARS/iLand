using System.Collections.Generic;

namespace iLand.Tools
{
    internal static class DictionaryExtensions
    {
        public static void AddToList<TKey, TValue>(this Dictionary<TKey, List<TValue>> dictionary, TKey key, TValue value) where TKey : notnull
        {
            if (dictionary.TryGetValue(key, out List<TValue>? valueList) == false)
            {
                valueList = new List<TValue>();
                dictionary.Add(key, valueList);
            }
            valueList.Add(value);
        }
    }
}
