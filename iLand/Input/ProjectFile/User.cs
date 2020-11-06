using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class User : XmlSerializable
    {
        public string Code { get; private set; }
        public float WindspeedFactor { get; private set; }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("user"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("code"))
            {
                this.Code = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("windspeed_factor"))
            {
                this.WindspeedFactor = reader.ReadElementContentAsFloat();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
