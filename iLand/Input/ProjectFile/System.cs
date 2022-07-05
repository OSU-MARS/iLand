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

            switch (reader.Name)
            {
                case "system":
                    reader.Read();
                    break;
                case "paths":
                    this.Paths.ReadXml(reader);
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
