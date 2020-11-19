using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class User : XmlSerializable
    {
        public string? Code { get; private set; }
        public float WindspeedFactor { get; private set; }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            if (String.Equals(reader.Name, "user", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "code", StringComparison.Ordinal))
            {
                this.Code = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "windspeedFactor", StringComparison.Ordinal))
            {
                this.WindspeedFactor = reader.ReadElementContentAsFloat();
                if (this.WindspeedFactor < 0.0F)
                {
                    throw new XmlException("Windspeed factor is negative.");
                }
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
