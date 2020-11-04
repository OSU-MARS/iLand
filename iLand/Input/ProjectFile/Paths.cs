using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Paths
    {
        [XmlElement(ElementName = "home")]
        public string Home { get; set; }

        [XmlElement(ElementName = "database")]
        public string Database { get; set; }

        [XmlElement(ElementName = "gis")]
        public string Gis { get; set; }

        [XmlElement(ElementName = "lip")]
        public string LightIntensityProfile { get; set; }

        [XmlElement(ElementName = "log")]
        public string Log { get; set; }

        [XmlElement(ElementName = "temp")]
        public string Temp { get; set; }

        [XmlElement(ElementName = "script")]
        public string Script { get; set; }

        [XmlElement(ElementName = "init")]
        public string Init { get; set; }

        [XmlElement(ElementName = "output")]
        public string Output { get; set; }

        public Paths()
        {
            this.Home = null;

            this.Database = "database";
            this.Gis = "gis";
            this.Init = "init";
            this.LightIntensityProfile = "lip";
            this.Log = "log";
            this.Output = "output";
            this.Script = "script";
            this.Temp = "temp";
        }
    }
}
