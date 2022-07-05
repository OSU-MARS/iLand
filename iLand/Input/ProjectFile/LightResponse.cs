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
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "lightResponse":
                    reader.Read();
                    break;
                case "shadeIntolerant":
                    this.ShadeIntolerant = reader.ReadElementContentAsString().Trim();
                    break;
                case "shadeTolerant":
                    this.ShadeTolerant = reader.ReadElementContentAsString().Trim();
                    break;
                case "relativeHeightLriModifier":
                    this.RelativeHeightLriModifier = reader.ReadElementContentAsString().Trim();
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
