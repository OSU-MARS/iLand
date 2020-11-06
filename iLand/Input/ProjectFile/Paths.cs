using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Paths : XmlSerializable
    {
        public string Home { get; private set; }
        public string Database { get; private set; }
        public string Gis { get; private set; }
        public string LightIntensityProfile { get; private set; }
        public string Log { get; private set; }
        public string Temp { get; private set; }
        public string Script { get; private set; }
        public string Init { get; private set; }
        public string Output { get; private set; }

        public Paths(string homePath)
        {
            this.Home = homePath;

            this.Database = "database";
            this.Gis = "gis";
            this.Init = "init";
            this.LightIntensityProfile = "lip";
            this.Log = "log";
            this.Output = "output";
            this.Script = "script";
            this.Temp = "temp";
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("path"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("home"))
            {
                // interpret an empty <home> element as an indication to continue defaulting to the project directory
                string candidateHomePath = reader.ReadElementContentAsString().Trim();
                if (String.IsNullOrEmpty(candidateHomePath) == false)
                {
                    this.Home = candidateHomePath;
                }
            }
            else if (reader.IsStartElement("database"))
            {
                this.Database = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("gis"))
            {
                this.Gis = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("lip"))
            {
                this.LightIntensityProfile = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("log"))
            {
                this.Log = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("temp"))
            {
                this.Temp = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("script"))
            {
                this.Script = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("init"))
            {
                this.Init = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("output"))
            {
                this.Output = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
