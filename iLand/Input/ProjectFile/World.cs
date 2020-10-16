using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class World
	{
		[XmlElement(ElementName = "cellSize")]
		public float CellSize { get; set; }

		[XmlElement(ElementName = "width")]
		public float Width { get; set; }

		[XmlElement(ElementName = "height")]
		public float Height { get; set; }

		[XmlElement(ElementName = "buffer")]
		public float Buffer { get; set; }

		// latitude of project site in degrees
		[XmlElement(ElementName = "latitude")]
		public float Latitude { get; set; }

		[XmlElement(ElementName = "resourceUnitsAsGrid")]
		public bool ResourceUnitsAsGrid { get; set; }

		[XmlElement(ElementName = "environmentEnabled")]
		public bool EnvironmentEnabled { get; set; }

		[XmlElement(ElementName = "environmentMode")]
		public string EnvironmentMode { get; set; }

		[XmlElement(ElementName = "environmentGrid")]
		public string EnvironmentGridFile { get; set; }

		[XmlElement(ElementName = "environmentFile")]
		public string EnvironmentFile { get; set; }

		[XmlElement(ElementName = "areaMask")]
		public AreaMask AreaMask { get; set; }

		[XmlElement(ElementName = "timeEventsEnabled")]
		public bool TimeEventsEnabled { get; set; }

		[XmlElement(ElementName = "timeEventsFile")]
		public string TimeEventsFile { get; set; }

		[XmlElement(ElementName = "location")]
		public Location Location { get; set; }

		[XmlElement(ElementName = "standGrid")]
		public StandGrid StandGrid { get; set; }

		[XmlElement(ElementName = "DEM")]
		public string DemFile { get; set; }

		public World()
        {
			// default to a single resource unit
			this.Buffer = 0.6F * Constant.RUSize;
			this.CellSize = Constant.LightSize;
			this.DemFile = null;
			this.EnvironmentEnabled = false;
			this.EnvironmentGridFile = null;
			this.EnvironmentMode = null;
			this.Height = Constant.RUSize;
			this.Latitude = 48.0F;
			this.ResourceUnitsAsGrid = false; // TODO: unhelpful default since must be set to true in project
			this.TimeEventsEnabled = false;
			this.TimeEventsFile = null;
			this.Width = Constant.RUSize;

			this.AreaMask = new AreaMask();
			this.Location = null;
			this.StandGrid = new StandGrid();
        }
	}
}
