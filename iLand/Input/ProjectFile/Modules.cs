using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Modules
	{
		[XmlElement(ElementName = "fire")]
		public Fire Fire { get; set; }

		[XmlElement(ElementName = "wind")]
		public Wind Wind { get; set; }

		[XmlElement(ElementName = "barkbeetle")]
		public BarkBeetle BarkBeetle { get; set; }
    }
}
