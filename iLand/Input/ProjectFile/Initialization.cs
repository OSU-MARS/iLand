using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Initialization
    {
		[XmlElement(ElementName = "mapFileName")]
		public string MapFileName { get; set; }

		[XmlElement(ElementName = "mode")]
		public string Mode { get; set; }

		[XmlElement(ElementName = "type")]
		public string Type { get; set; }

		[XmlElement(ElementName = "randomFunction")]
		public string RandomFunction { get; set; }

		[XmlElement(ElementName = "file")]
		public string File { get; set; }

		[XmlElement(ElementName = "saplingFile")]
		public string SaplingFile { get; set; }

		[XmlElement(ElementName = "snags")]
		public Snags Snags { get; set; }

		[XmlElement(ElementName = "heightGrid")]
		public HeightGrid HeightGrid { get; set; }

		public Initialization()
        {
			this.File = null;
			this.MapFileName = "init";
			this.Mode = "copy";
			this.RandomFunction = "1-x^2";
			this.SaplingFile = null;
			this.Type = null;

			this.HeightGrid = new HeightGrid();
			this.Snags = new Snags();
        }
	}
}
