using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ConditionOutput : Enablable
    {
        public string? Condition { get; protected set; }

        public ConditionOutput()
        {
            this.Condition = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "landscape", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "sapling", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "stand", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "condition", StringComparison.Ordinal))
            {
                this.Condition = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
