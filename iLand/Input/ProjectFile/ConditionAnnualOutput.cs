using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ConditionAnnualOutput : Enablable
    {
        public string? Condition { get; protected set; }

        public ConditionAnnualOutput(string elementName)
            : base(elementName)
        {
            this.Condition = null;
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
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
