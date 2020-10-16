using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Logging
    {
        [XmlElement(ElementName = "logTarget")]
        public string LogTarget { get; set; }

        [XmlElement(ElementName = "logFile")]
        public string LogFile { get; set; }

        [XmlElement(ElementName = "flush")]
        public bool Flush { get; set; }
    }
}
