using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
	public class WorldGeometry : XmlSerializable
	{
		public int BufferWidthInM { get; private set; }
		public string DigitalElevationModel { get; private set; }

		// special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
		public bool IsTorus { get; private set; }
		// latitude of project site in degrees
		public float Latitude { get; private set; }

		public WorldGeometry()
		{
			this.BufferWidthInM = Constant.Grid.DefaultWorldBufferWidthInM;
			this.DigitalElevationModel = String.Empty;
			this.Latitude = Single.NaN;
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
					int bufferWidthInM = reader.ReadElementContentAsInt();
					int maxLightStampSizeInM = Constant.Grid.MaxLightStampSizeInLightCells / 2 * Constant.Grid.LightCellSizeInM;
                    if ((bufferWidthInM < maxLightStampSizeInM) || (bufferWidthInM % Constant.Grid.LightCellSizeInM != 0) || (bufferWidthInM % Constant.Grid.HeightCellSizeInM != 0))
					{
						throw new XmlException("Buffer width of " + bufferWidthInM + " m is not a positive, integer multiple of the light and height cell sizes (" + Constant.Grid.LightCellSizeInM + " and " + Constant.Grid.HeightCellSizeInM + " m, respectively) which is greater than the " + maxLightStampSizeInM + " m radius of the largest tree stamp available. If regeneration is enabled an integer multiple of the seedmap cell size (" + Constant.Grid.SeedmapCellSizeInM + " m) is also required. The default buffer width of 80 m meets all of these criteria and, typically, there is little reason to change it.");
					}
					this.BufferWidthInM = bufferWidthInM;
					break;
				case "geometry":
					reader.Read();
					break;
				case "torus":
					this.IsTorus = reader.ReadElementContentAsBoolean();
					break;
				case "latitude":
                    // TODO: large simulation areas can cover ~0.4° of latitude, resulting in ±0.2° of error from a central latitude choice
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

		public void Validate()
		{
            // this.BufferWidthInM is defaulted
            // this.DigitalElevationModel can remain empty;
            if (Single.IsNaN(this.Latitude))
			{
				throw new XmlException("Project area's latitude (/project/world/geometry/latitude) is not specified. The study site's latitude is required for .");
			}
            // this.IsTorus is boolean;
        }
    }
}