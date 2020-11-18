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

        public Paths(string? defaultHomePath)
        {
            if (String.IsNullOrWhiteSpace(defaultHomePath))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultHomePath));
            }

            this.Home = defaultHomePath;

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

            if (String.Equals(reader.Name, "paths", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "home", StringComparison.Ordinal))
            {
                // interpret an empty <home> element as an indication to continue defaulting to the project directory
                string candidateHomePath = reader.ReadElementContentAsString().Trim();
                if (String.IsNullOrEmpty(candidateHomePath) == false)
                {
                    this.Home = candidateHomePath;
                }
            }
            else if (String.Equals(reader.Name, "database", StringComparison.Ordinal))
            {
                this.Database = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "gis", StringComparison.Ordinal))
            {
                this.Gis = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "lip", StringComparison.Ordinal))
            {
                this.LightIntensityProfile = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "log", StringComparison.Ordinal))
            {
                this.Log = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "temp", StringComparison.Ordinal))
            {
                this.Temp = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "script", StringComparison.Ordinal))
            {
                this.Script = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "init", StringComparison.Ordinal))
            {
                this.Init = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "output", StringComparison.Ordinal))
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
