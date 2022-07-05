using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class World : XmlSerializable
	{
		public Browsing Browsing { get; private init; }
		public Climate Climate { get; private init; }
		public WorldDebug Debug { get; private init; }
		public DefaultSoil DefaultSoil { get; private init; }
		public WorldGeometry Geometry { get; private init; }
		public Grass Grass { get; private init; }
		public WorldInitialization Initialization { get; private init; }
		public Snag Snag { get; private init; }
		public Species Species { get; private init; }

		public World()
        {
			this.Browsing = new();
			this.Climate = new();
			this.Debug = new();
			this.DefaultSoil = new();
			this.Grass = new();
			this.Geometry = new();
			this.Initialization = new();
			this.Snag = new();
			this.Species = new();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				switch (reader.Name)
				{
					case "browsing":
						this.Browsing.ReadXml(reader);
						break;
					case "grass":
						this.Grass.ReadXml(reader);
						break;
					default:
						throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else
			{
				switch (reader.Name)
				{
					case "world":
						reader.Read();
						break;
					case "climate":
						this.Climate.ReadXml(reader);
						break;
					case "debug":
						this.Debug.ReadXml(reader);
						break;
					case "defaultSoil":
						this.DefaultSoil.ReadXml(reader);
						break;
					case "geometry":
						this.Geometry.ReadXml(reader);
						break;
					case "initialization":
						this.Initialization.ReadXml(reader);
						break;
					case "snag":
						this.Snag.ReadXml(reader);
						break;
					case "species":
						this.Species.ReadXml(reader);
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
		}
	}
}
