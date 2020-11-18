using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class LightResponse : XmlSerializable
    {
        public string? ShadeIntolerant { get; private set; }
        public string? ShadeTolerant { get; private set; }
        public string? RelativeHeightLriModifier { get; private set; }

        public LightResponse()
        {
            // no defaults in C++, must be set in project file
            this.ShadeIntolerant = null;
            this.ShadeTolerant = null;

            this.RelativeHeightLriModifier = "1";
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "lightResponse", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "shadeIntolerant", StringComparison.Ordinal))
            {
                this.ShadeIntolerant = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "shadeTolerant", StringComparison.Ordinal))
            {
                this.ShadeTolerant = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "relativeHeightLriModifier", StringComparison.Ordinal))
            {
                this.RelativeHeightLriModifier = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
