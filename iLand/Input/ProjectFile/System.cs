using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class System : XmlSerializable
    {
        public Paths Path { get; private set; }
        public Database Database { get; private set; }
        public Logging Logging { get; private set; }
        public SystemSettings Settings { get; private set; }

        // not currently supported
        //<javascript>
        //	<fileName></fileName>
        //</javascript>

        public System(string? defaultHomePath)
        {
            this.Path = new Paths(defaultHomePath);
            this.Database = new Database();
            this.Logging = new Logging();
            this.Settings = new SystemSettings();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("system"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("path"))
            {
                this.Path.ReadXml(reader);
            }
            else if (reader.IsStartElement("database"))
            {
                this.Database.ReadXml(reader);
            }
            else if (reader.IsStartElement("logging"))
            {
                this.Logging.ReadXml(reader);
            }
            else if (reader.IsStartElement("settings"))
            {
                this.Settings.ReadXml(reader);
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
