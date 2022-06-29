using System;
using System.Diagnostics;
using System.Xml;

namespace iLand.Input.ProjectFile
{
	public class WorldGeometry : XmlSerializable
	{
		public float Buffer { get; private set; }
		public float LightCellSize { get; private set; }
		public float Width { get; private set; }
		public float Height { get; private set; }

		// special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
		public bool IsTorus { get; private set; }
		// latitude of project site in degrees
		public float Latitude { get; private set; }

		public WorldGeometry()
		{
			// default to a single resource unit
			this.Buffer = 0.6F * Constant.RUSize;
			this.LightCellSize = Constant.LightCellSizeInM;
			this.Height = Constant.RUSize;
			this.Latitude = 48.0F;
			this.IsTorus = false;
			this.Width = Constant.RUSize;
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			if (String.Equals(reader.Name, "geometry", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "lightCellSize", StringComparison.Ordinal))
			{
				this.LightCellSize = reader.ReadElementContentAsFloat();
				if (this.LightCellSize <= 0.0F)
				{
					throw new XmlException("Light cell size is zero or negative.");
				}
			}
			else if (String.Equals(reader.Name, "torus", StringComparison.Ordinal))
			{
				this.IsTorus = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "width", StringComparison.Ordinal))
			{
				this.Width = reader.ReadElementContentAsFloat();
				if (this.Width <= 0.0F)
				{
					throw new XmlException("Model width is zero or negative.");
				}
			}
			else if (String.Equals(reader.Name, "height", StringComparison.Ordinal))
			{
				this.Height = reader.ReadElementContentAsFloat();
				if (this.Height <= 0.0F)
				{
					throw new XmlException("Model height is zero or negative.");
				}
			}
			else if (String.Equals(reader.Name, "buffer", StringComparison.Ordinal))
			{
				this.Buffer = reader.ReadElementContentAsFloat();
				if (this.Buffer <= 0.0F)
				{
					throw new XmlException("Light buffer width is zero or negative.");
				}
			}
			else if (String.Equals(reader.Name, "latitude", StringComparison.Ordinal))
			{
				this.Latitude = reader.ReadElementContentAsFloat();
				if ((this.Latitude < -90.0F) || (this.Latitude > 90.0F))
				{
					throw new XmlException("Latitude is not between -90 and 90°.");
				}
			}
			else
			{
				throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}