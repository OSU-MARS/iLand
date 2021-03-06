﻿using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class World : XmlSerializable
	{
		public string? DemFile { get; private set; }
		public string? EnvironmentFile { get; private set; }
		public string? EnvironmentGridFile { get; private set; }

		public AreaMask AreaMask { get; private init; }
		public Browsing Browsing { get; private init; }
		public Climate Climate { get; private init; }
		public WorldDebug Debug { get; private init; }
		public DefaultSoil DefaultSoil { get; private init; }
		public WorldGeometry Geometry { get; private init; }
		public Grass Grass { get; private init; }
		public WorldInitialization Initialization { get; private init; }
		public Species Species { get; private init; }
		public StandGrid StandGrid { get; private init; }

		public World()
        {
			this.DemFile = null;
			this.EnvironmentGridFile = null;

			this.AreaMask = new AreaMask();
			this.Browsing = new Browsing();
			this.Climate = new Climate();
			this.Debug = new WorldDebug();
			this.DefaultSoil = new DefaultSoil();
			this.Grass = new Grass();
			this.Geometry = new WorldGeometry();
			this.Initialization = new WorldInitialization();
			this.Species = new Species();
			this.StandGrid = new StandGrid();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				if (String.Equals(reader.Name, "areaMask", StringComparison.Ordinal))
				{
					this.AreaMask.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "browsing", StringComparison.Ordinal))
				{
					this.Browsing.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "grass", StringComparison.Ordinal))
				{
					this.Grass.ReadXml(reader);
				}
				else
				{
					throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else if (String.Equals(reader.Name, "world", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "climate", StringComparison.Ordinal))
			{
				this.Climate.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "debug", StringComparison.Ordinal))
			{
				this.Debug.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "defaultSoil", StringComparison.Ordinal))
			{
				this.DefaultSoil.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "dem", StringComparison.Ordinal))
			{
				this.DemFile = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "environmentGrid", StringComparison.Ordinal))
			{
				this.EnvironmentGridFile = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "environmentFile", StringComparison.Ordinal))
			{
				this.EnvironmentFile = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "geometry", StringComparison.Ordinal))
			{
				this.Geometry.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "initialization", StringComparison.Ordinal))
			{
				this.Initialization.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "species", StringComparison.Ordinal))
			{
				this.Species.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "standGrid", StringComparison.Ordinal))
			{
				this.StandGrid.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
