using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Browsing : Enablable
    {
        public double BrowsingPressure { get; private set; }

        public Browsing()
        {
            this.BrowsingPressure = 0.0;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("browsing"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("browsingPressure"))
            {
                this.BrowsingPressure = reader.ReadElementContentAsDouble();
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
