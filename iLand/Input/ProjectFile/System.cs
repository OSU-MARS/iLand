using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class System
    {
        [XmlElement(ElementName = "path")]
        public Paths Path { get; set; }

        [XmlElement(ElementName = "database")]
        public Database Database { get; set; }

        [XmlElement(ElementName = "logging")]
        public Logging Logging { get; set; }

        [XmlElement(ElementName = "settings")]
        public SystemSettings Settings { get; set; }

        // not currently supported
	    //<javascript>
	    //	<fileName></fileName>
	    //</javascript>
    }
}
