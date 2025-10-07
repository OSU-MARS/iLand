using System;
using System.Xml;

namespace iLand.Extensions
{
    public static class XmlReaderExtensions
    {
        public static float[] ReadElementContentAsFloatArray(this XmlReader reader)
        {
            string[] valuesAsStrings = reader.ReadElementContentAsString().Split(separator: null); // split by whitespace
            float[] values = new float[valuesAsStrings.Length];
            for (int index = 0; index < valuesAsStrings.Length; ++index)
            {
                values[index] = Single.Parse(valuesAsStrings[index]);
            }

            return values;
        }
    }
}
