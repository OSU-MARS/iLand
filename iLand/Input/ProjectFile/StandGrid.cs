using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class StandGrid : XmlSerializable
    {
        public string? FileName { get; private set; }

        public StandGrid()
        {
            this.FileName = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            if (String.Equals(reader.Name, "standGrid", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "fileName", StringComparison.Ordinal))
            {
                this.FileName = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
