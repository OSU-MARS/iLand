using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class AgentBasedEngine
    {
        [XmlElement(ElementName = "file")]
        public string File { get; set; }

        [XmlElement(ElementName = "agentDataFile")]
        public string AgentDataFile { get; set; }
    }
}
