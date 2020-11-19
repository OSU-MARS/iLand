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

            if (String.Equals(reader.Name, "abe", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "fileName", StringComparison.Ordinal))
            {
                this.FileName = reader.ReadElementContentAsString().Trim();
                if (String.IsNullOrEmpty(this.FileName) == false)
                {
                    throw new NotImplementedException();
                }
            }
            else if (String.Equals(reader.Name, "agentDataFile", StringComparison.Ordinal))
            {
                this.AgentDataFile = reader.ReadElementContentAsString().Trim();
                if (String.IsNullOrEmpty(this.AgentDataFile) == false)
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
