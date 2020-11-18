using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class FilterOutput : Enablable
    {
        public string? Filter { get; private set; }

        public FilterOutput()
        {
            this.Filter = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "tree", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "treeRemoved", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "filter", StringComparison.Ordinal))
            {
                this.Filter = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
