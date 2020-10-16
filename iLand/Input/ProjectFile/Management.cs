using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Management : Enablable
    {
        [XmlElement(ElementName = "file")]
        public string File { get; set; }

        [XmlElement(ElementName = "abeEnabled")]
        public bool AbeEnabled { get; set; }

        [XmlElement(ElementName = "abe")]
        public AgentBasedEngine Abe { get; set; }

        public Management()
        {
            // no default in C++
            // this.Enabled

            this.Abe = new AgentBasedEngine();
        }
    }
}
