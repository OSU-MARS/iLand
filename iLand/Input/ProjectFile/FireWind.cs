using System;
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
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (String.Equals(reader.Name, "wind", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "speedMin", StringComparison.Ordinal))
			{
				this.SpeedMin = reader.ReadElementContentAsFloat();
				if (this.SpeedMin < 0.0F)
                {
					throw new XmlException("Minimum wind speed is negative.");
                }
			}
			else if (String.Equals(reader.Name, "speedMax", StringComparison.Ordinal))
			{
				this.SpeedMax = reader.ReadElementContentAsFloat();
				if (this.SpeedMax < 0.0F)
				{
					throw new XmlException("Maximum wind speed is negative.");
				}
			}
			else if (String.Equals(reader.Name, "direction", StringComparison.Ordinal))
			{
				this.Direction = reader.ReadElementContentAsFloat();
				if ((this.Direction < 0.0F) || (this.Direction > 360.0F))
				{
					throw new XmlException("Default wind direction is not between 0 and 360°, inclusive.");
				}
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
