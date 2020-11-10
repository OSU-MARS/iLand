using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class LightResponse : XmlSerializable
    {
        public string? ShadeIntolerant { get; private set; }
        public string? ShadeTolerant { get; private set; }
        public string? LriModifier { get; private set; }

        public LightResponse()
        {
            // no defaults in C++, must be set in project file
            this.ShadeIntolerant = null;
            this.ShadeTolerant = null;

            this.LriModifier = "1";
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("lightResponse"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("shadeIntolerant"))
            {
                this.ShadeIntolerant = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("shadeTolerant"))
            {
                this.ShadeTolerant = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("LRImodifier"))
            {
                this.LriModifier = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
