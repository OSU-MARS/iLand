using iLand.Tools;
using System.IO;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    [XmlRoot(ElementName = "project")]
    public class Project
    {
        [XmlElement(ElementName = "system")]
		public System System { get; set; }

		[XmlElement(ElementName = "model")]
        public Model Model { get; set; }

		[XmlElement(ElementName = "output")]
		public Outputs Output { get; set; }

		[XmlElement(ElementName = "modules")]
		public Modules Modules { get; set; }

		[XmlElement(ElementName = "user")]
		public User User { get; set; }

		public Project()
        {
			this.Model = new Model();
			this.Modules = new Modules();
			this.Output = new Outputs();
			this.System = new System();
			this.User = new User();
        }

		public static Project Load(string xmlFilePath)
		{
			XmlConformingSerializer deserializer = new XmlConformingSerializer(typeof(Project));
			using FileStream stream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			return deserializer.Deserialize<Project>(stream);
		}
	}
}