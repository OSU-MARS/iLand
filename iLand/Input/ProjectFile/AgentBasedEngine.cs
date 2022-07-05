using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class AgentBasedEngine : XmlSerializable
    {
        public string? AgentDataFile { get; private set; }
        public string? FileName { get; private set; }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "abe":
                    reader.Read();
                    break;
                case "fileName":
                    this.FileName = reader.ReadElementContentAsString().Trim();
                    if (String.IsNullOrEmpty(this.FileName) == false)
                    {
                        throw new NotImplementedException();
                    }
                    break;
                case "agentDataFile":
                    this.AgentDataFile = reader.ReadElementContentAsString().Trim();
                    if (String.IsNullOrEmpty(this.AgentDataFile) == false)
                    {
                        throw new NotImplementedException();
                    }
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
