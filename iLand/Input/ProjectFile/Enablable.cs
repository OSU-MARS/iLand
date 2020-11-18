using System;
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

            if (String.Equals(reader.Name, "productionMonth", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "management", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "barkbeetle", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "wind", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "fire", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "standDead", StringComparison.Ordinal))
            {
                reader.Read();
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
