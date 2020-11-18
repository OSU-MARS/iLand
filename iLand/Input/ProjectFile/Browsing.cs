using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Browsing : Enablable
    {
        public float BrowsingPressure { get; private set; }

        public Browsing()
        {
            this.BrowsingPressure = 1.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "browsing", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "browsingPressure", StringComparison.Ordinal))
            {
                this.BrowsingPressure = reader.ReadElementContentAsFloat();
                // no clear restriction on range of value
            }
            else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
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
