using System;
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

            if (String.Equals(reader.Name, "areaMask", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "imageFile", StringComparison.Ordinal))
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
