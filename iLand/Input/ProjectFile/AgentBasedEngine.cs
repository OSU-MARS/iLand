using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class AgentBasedEngine : XmlSerializable
    {
        public string File { get; private set; }
        public string AgentDataFile { get; private set; }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("abe"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("file"))
            {
                this.File = reader.ReadElementContentAsString().Trim();
                if (String.IsNullOrEmpty(this.File) == false)
                {
                    throw new NotImplementedException();
                }
            }
            else if (reader.IsStartElement("agentDataFile"))
            {
                this.AgentDataFile = reader.ReadElementContentAsString().Trim();
                if (String.IsNullOrEmpty(this.AgentDataFile) == false)
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
