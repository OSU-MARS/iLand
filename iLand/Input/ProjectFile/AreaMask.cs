using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class AreaMask : Enablable
    {
        public string? ImageFile { get; private set; }

        public AreaMask()
        {
            this.ImageFile = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("areaMask"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("imageFile"))
            {
                this.ImageFile = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
