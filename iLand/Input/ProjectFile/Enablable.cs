using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Enablable
    {
        [XmlElement(ElementName = "enabled")]
        public bool Enabled { get; set; }

        public Enablable()
        {
            this.Enabled = false;
        }
    }
}
