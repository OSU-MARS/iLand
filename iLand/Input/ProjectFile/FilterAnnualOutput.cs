using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class FilterAnnualOutput : Enablable
    {
        public string? Filter { get; private set; }

        public FilterAnnualOutput(string elementName)
            : base(elementName)
        {
            this.Filter = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                this.ReadEnabled(reader);
            }
            else if (String.Equals(reader.Name, "filter", StringComparison.Ordinal))
            {
                this.Filter = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
