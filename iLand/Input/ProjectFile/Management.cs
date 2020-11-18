using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Management : XmlSerializable
    {
        public AgentBasedEngine Abe { get; init; }
        public bool AbeEnabled { get; private set; }
        public string? FileName { get; private set; }

        public Management()
        {
            this.Abe = new AgentBasedEngine();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "management", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "fileName", StringComparison.Ordinal))
            {
                this.FileName = reader.ReadElementContentAsString().Trim();
                if (String.IsNullOrEmpty(this.FileName) == false)
                {
                    throw new NotImplementedException("Management files are not currently read.");
                }
            }
            else if (String.Equals(reader.Name, "abeEnabled", StringComparison.Ordinal))
            {
                this.AbeEnabled = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "abe", StringComparison.Ordinal))
            {
                this.Abe.ReadXml(reader);
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
