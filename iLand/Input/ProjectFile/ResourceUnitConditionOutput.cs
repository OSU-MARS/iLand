using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ResourceUnitConditionOutput : ConditionOutput
    {
        public string? ConditionRU { get; private set; }

        public ResourceUnitConditionOutput()
        {
            this.ConditionRU = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "carbon", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "carbonFlow", StringComparison.Ordinal) ||
                String.Equals(reader.Name, "water", StringComparison.Ordinal))
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
            else if (String.Equals(reader.Name, "conditionRU", StringComparison.Ordinal))
            {
                this.ConditionRU = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
