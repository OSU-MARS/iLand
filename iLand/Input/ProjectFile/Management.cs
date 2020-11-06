using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Management : Enablable
    {
        public string File { get; private set; }
        public bool AbeEnabled { get; private set; }
        public AgentBasedEngine Abe { get; private set; }

        public Management()
        {
            // no default in C++
            // this.Enabled

            this.Abe = new AgentBasedEngine();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("management"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("file"))
            {
                this.File = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("abeEnabled"))
            {
                this.AbeEnabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("abe"))
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
