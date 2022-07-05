using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Browsing : Enablable
    {
        public float BrowsingPressure { get; private set; }

        public Browsing()
            : base("browsing")
        {
            this.BrowsingPressure = 1.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                this.ReadEnabled(reader);
            }
            else
            {
                switch (reader.Name)
                {
                    case "browsingPressure":
                        this.BrowsingPressure = reader.ReadElementContentAsFloat();
                        // no clear restriction on range of value
                        break;
                    default:
                        throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
                }
            }
        }
    }
}
