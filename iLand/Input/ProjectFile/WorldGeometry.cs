using System.Xml;

namespace iLand.Input.ProjectFile
{
	public class WorldGeometry : XmlSerializable
	{
		public int BufferWidth { get; private set; }

		// special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
		public bool IsTorus { get; private set; }
		// latitude of project site in degrees
		public float Latitude { get; private set; }

		public WorldGeometry()
		{
			// default to a single resource unit
			this.BufferWidth = (int)(0.6F * Constant.ResourceUnitSizeInM);
			this.Latitude = 48.0F;
			this.IsTorus = false;
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			switch (reader.Name)
			{
				case "bufferWidth":
					this.BufferWidth = reader.ReadElementContentAsInt();
					if ((this.BufferWidth < Constant.LightCellSizeInM) || (this.BufferWidth % Constant.LightCellSizeInM != 0))
					{
						throw new XmlException("Light buffer width must be a positive,integer multiple of the light cell size (" + Constant.LightCellSizeInM + " m).");
					}
					break;
				case "geometry":
					reader.Read();
					break;
				case "torus":
					this.IsTorus = reader.ReadElementContentAsBoolean();
					break;
				case "latitude":
					// TODO: large simulation areas can cover ~0.4° of longitude, resulting in ±0.2° of error from a central longitude choice
					this.Latitude = reader.ReadElementContentAsFloat();
					if ((this.Latitude <= -90.0F) || (this.Latitude >= 90.0F))
					{
						throw new XmlException("Latitude is not between -90 and 90°.");
					}
					break;
				default:
					throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}