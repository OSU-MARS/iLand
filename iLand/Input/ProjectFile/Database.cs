using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Database
    {
        [XmlElement(ElementName = "in")]
        public string In { get; set; }

        [XmlElement(ElementName = "out")]
        public string Out { get; set; }

        [XmlElement(ElementName = "climate")]
        public string Climate { get; set; }

        public Database()
        {
            this.Out = null;
        }
    }
}
