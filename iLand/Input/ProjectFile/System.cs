using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class System : XmlSerializable
    {
        public Paths Paths { get; private init; }

        // not currently supported
        //<javascript>
        //	<fileName></fileName>
        //</javascript>

        public System(string? defaultHomePath)
        {
            this.Paths = new Paths(defaultHomePath);
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            if (String.Equals(reader.Name, "system", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "paths", StringComparison.Ordinal))
            {
                this.Paths.ReadXml(reader);
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
