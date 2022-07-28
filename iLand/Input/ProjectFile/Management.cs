using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Management : XmlSerializable
    {
        public AgentBasedEngine Abe { get; private init; }
        public bool AbeEnabled { get; private set; }
        public string? FileName { get; private set; }

        public Management()
        {
            this.Abe = new();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "management":
                    reader.Read();
                    break;
                case "fileName":
                    this.FileName = reader.ReadElementContentAsString().Trim();
                    if (String.IsNullOrEmpty(this.FileName) == false)
                    {
                        throw new NotImplementedException("Management files are not currently read.");
                    }
                    break;
                case "abeEnabled":
                    this.AbeEnabled = reader.ReadElementContentAsBoolean();
                    break;
                case "abe":
                    this.Abe.ReadXml(reader);
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
