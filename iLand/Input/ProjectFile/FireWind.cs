using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class FireWind : XmlSerializable
    {
        public float SpeedMin { get; private set; }
        public float SpeedMax { get; private set; }
        public float Direction { get; private set; }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			switch (reader.Name)
			{
				case "wind":
					reader.Read();
					break;
				case "speedMin":
					this.SpeedMin = reader.ReadElementContentAsFloat();
					if (this.SpeedMin < 0.0F)
					{
						throw new XmlException("Minimum wind speed is negative.");
					}
					break;
				case "speedMax":
					this.SpeedMax = reader.ReadElementContentAsFloat();
					if (this.SpeedMax < 0.0F)
					{
						throw new XmlException("Maximum wind speed is negative.");
					}
					break;
				case "direction":
					this.Direction = reader.ReadElementContentAsFloat();
					if ((this.Direction < 0.0F) || (this.Direction > 360.0F))
					{
						throw new XmlException("Default wind direction is not between 0 and 360°, inclusive.");
					}
					break;
				default:
					throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
