using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Enablable : XmlSerializable
    {
        private readonly string elementName;

        public bool Enabled { get; protected set; }

        public Enablable(string elementName)
        {
            this.elementName = elementName;

            this.Enabled = false;
        }

        protected void ReadEnabled(XmlReader reader)
        {
            if (String.Equals(reader.Name, elementName, StringComparison.Ordinal))
            {
                if (reader.AttributeCount != 1)
                {
                    throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
                }

                string? enabledAsString = reader.GetAttribute("enabled");
                if (String.IsNullOrWhiteSpace(enabledAsString))
                {
                    throw new XmlException("enabled attribute of " + reader.Name + " is empty.");
                }
                this.Enabled = Boolean.Parse(enabledAsString);
                reader.ReadStartElement();
            }
            else
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                this.ReadEnabled(reader);
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
