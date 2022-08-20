using iLand.Tree;
using System.Xml;

namespace iLand.Input.ProjectFile
{
	public class WorldGeometry : XmlSerializable
	{
		public int BufferWidthInM { get; private set; }

		// special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
		public bool IsTorus { get; private set; }
		// latitude of project site in degrees
		public float Latitude { get; private set; }

		public WorldGeometry()
		{
			// default to a single resource unit
			this.BufferWidthInM = 3 * Constant.SeedmapCellSizeInM;
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
					this.BufferWidthInM = reader.ReadElementContentAsInt();
					if ((this.BufferWidthInM < Constant.HeightCellSizeInM) || (this.BufferWidthInM % Constant.LightCellSizeInM != 0) || (this.BufferWidthInM % Constant.HeightCellSizeInM != 0))
					{
						throw new XmlException("Light buffer width must be a positive, integer multiple of the light and height cell sizes (" + Constant.LightCellSizeInM + " and " + Constant.HeightCellSizeInM + " m, respectively) which is greater than the radius of the largest tree stamp used by the model (potentially up to " + ((int)LightStampSize.Grid64x64 / 2 * Constant.LightCellSizeInM) + " m). If regeneration is enabled integer multiples of the seedmap cell size (" + Constant.SeedmapCellSizeInM + " m) are also required.");
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