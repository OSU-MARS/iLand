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
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "paths":
                    reader.Read();
                    break;
                case "home":
                    // interpret an empty <home> element as an indication to continue defaulting to the project directory
                    string candidateHomePath = reader.ReadElementContentAsString().Trim();
                    if (String.IsNullOrEmpty(candidateHomePath) == false)
                    {
                        this.Home = candidateHomePath;
                    }
                    break;
                case "database":
                    this.Database = reader.ReadElementContentAsString().Trim();
                    break;
                case "gis":
                    this.Gis = reader.ReadElementContentAsString().Trim();
                    break;
                case "lip":
                    this.LightIntensityProfile = reader.ReadElementContentAsString().Trim();
                    break;
                case "log":
                    this.Log = reader.ReadElementContentAsString().Trim();
                    break;
                case "temp":
                    this.Temp = reader.ReadElementContentAsString().Trim();
                    break;
                case "script":
                    this.Script = reader.ReadElementContentAsString().Trim();
                    break;
                case "init":
                    this.Init = reader.ReadElementContentAsString().Trim();
                    break;
                case "output":
                    this.Output = reader.ReadElementContentAsString().Trim();
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
