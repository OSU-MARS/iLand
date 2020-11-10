using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Enablable : XmlSerializable
    {
        public bool Enabled { get; protected set; }

        public Enablable()
        {
            this.Enabled = false;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("productionMonth") ||
                reader.IsStartElement("management") ||
                reader.IsStartElement("barkbeetle") ||
                reader.IsStartElement("wind") ||
                reader.IsStartElement("fire") ||
                reader.IsStartElement("standDead"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
