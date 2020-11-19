using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ResourceUnitConditionOutput : ConditionAnnualOutput
    {
        public string? ConditionRU { get; private set; }

        public ResourceUnitConditionOutput(string elementName)
            : base(elementName)
        {
            this.ConditionRU = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                this.ReadEnabled(reader);
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
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
