using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Model
    {
		[XmlElement(ElementName = "settings")]
		public ModelSettings Settings { get; set; }

		[XmlElement(ElementName = "species")]
		public Species Species { get; set; }

		[XmlElement(ElementName = "world")]
		public World World { get; set; }

		[XmlElement(ElementName = "site")]
		public Site Site { get; set; }

		[XmlElement(ElementName = "climate")]
		public Climate Climate { get; set; }

		[XmlElement(ElementName = "initialization")]
		public Initialization Initialization { get; set; }

		[XmlElement(ElementName = "management")]
		public Management Management { get; set; }

		[XmlElement(ElementName = "parameter")]
		public Parameter Parameter { get; set; }

		public Model()
        {
			this.Climate = new Climate();
			this.Initialization = new Initialization();
			this.Management = new Management();
			this.Parameter = new Parameter();
			this.Settings = new ModelSettings();
			this.Site = new Site();
			this.Species = new Species();
			this.World = new World();
        }
    }
}
