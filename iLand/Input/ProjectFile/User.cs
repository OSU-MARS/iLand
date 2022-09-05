using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class User : XmlSerializable
    {
        public string? Code { get; private set; } // currently unused
        public float WindspeedFactor { get; private set; } // currently unused

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "user":
                    reader.Read();
                    break;
                case "code":
                    this.Code = reader.ReadElementContentAsString().Trim();
                    break;
                case "windspeedFactor":
                    this.WindspeedFactor = reader.ReadElementContentAsFloat();
                    if (this.WindspeedFactor < 0.0F)
                    {
                        throw new XmlException("Windspeed factor is negative.");
                    }
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
