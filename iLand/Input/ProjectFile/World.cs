using System.Diagnostics;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class World : XmlSerializable
	{
		public float CellSize { get; private set; }
		public float Width { get; private set; }
		public float Height { get; private set; }
		public float Buffer { get; private set; }
		
		// latitude of project site in degrees
		public float Latitude { get; private set; }
		
		public bool ResourceUnitsAsGrid { get; private set; }
		public bool EnvironmentEnabled { get; private set; }
		public string? EnvironmentMode { get; private set; } // TODO: change to enum
		public string? EnvironmentGridFile { get; private set; }
		public string? EnvironmentFile { get; private set; }
		public AreaMask AreaMask { get; private set; }
		public bool TimeEventsEnabled { get; private set; }
		public string? TimeEventsFile { get; private set; }
		public Location? Location { get; private set; }
		public StandGrid StandGrid { get; private set; }
		public string? DemFile { get; private set; }

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

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("world"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("cellSize"))
			{
				this.CellSize = reader.ReadElementContentAsFloat();
				if (this.CellSize <= 0.0F)
				{
					throw new XmlException("Light cell size is zero or negative.");
				}
			}
			else if (reader.IsStartElement("width"))
			{
				this.Width = reader.ReadElementContentAsFloat();
				if (this.Width <= 0.0F)
				{
					throw new XmlException("Model width is zero or negative.");
				}
			}
			else if (reader.IsStartElement("height"))
			{
				this.Height = reader.ReadElementContentAsFloat();
				if (this.Height <= 0.0F)
				{
					throw new XmlException("Model height is zero or negative.");
				}
			}
			else if (reader.IsStartElement("buffer"))
			{
				this.Buffer = reader.ReadElementContentAsFloat();
				if (this.Buffer <= 0.0F)
				{
					throw new XmlException("Light buffer width is zero or negative.");
				}
			}
			else if (reader.IsStartElement("latitude"))
			{
				this.Latitude = reader.ReadElementContentAsFloat();
				if ((this.Latitude < -90.0F) || (this.Latitude > 90.0F))
				{
					throw new XmlException("Latitude is not between -90 and 90°.");
				}
			}
			else if (reader.IsStartElement("resourceUnitsAsGrid"))
			{
				this.ResourceUnitsAsGrid = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("environmentEnabled"))
			{
				this.EnvironmentEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("environmentMode"))
			{
				this.EnvironmentMode = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("environmentGrid"))
			{
				this.EnvironmentGridFile = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("environmentFile"))
			{
				this.EnvironmentFile = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("areaMask"))
			{
				this.AreaMask.ReadXml(reader);
			}
			else if (reader.IsStartElement("timeEventsEnabled"))
			{
				this.TimeEventsEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("timeEventsFile"))
			{
				this.TimeEventsFile = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("location"))
			{
				Debug.Assert(this.Location == null);
				this.Location = new Location();
				this.Location.ReadXml(reader);
			}
			else if (reader.IsStartElement("standGrid"))
			{
				this.StandGrid.ReadXml(reader);
			}
			else if (reader.IsStartElement("DEM"))
			{
				this.DemFile = reader.ReadElementContentAsString().Trim();
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
