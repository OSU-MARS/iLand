using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Database : XmlSerializable
    {
        public string? Output { get; private set; }
        public string? Climate { get; private set; }
        public string? Species { get; private set; }

        public Database()
        {
            this.Climate = null;
            this.Output = null;
            this.Species = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("database"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("climate"))
            {
                this.Climate = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("output"))
            {
                this.Output = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("species"))
            {
                this.Species = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
