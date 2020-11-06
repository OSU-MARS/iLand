using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class FireWind : XmlSerializable
    {
        public double SpeedMin { get; private set; }
        public double SpeedMax { get; private set; }
        public double Direction { get; private set; }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("wind"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("speedMin"))
			{
				this.SpeedMin = reader.ReadElementContentAsDouble();
				if (this.SpeedMin < 0.0)
                {
					throw new XmlException("Minimum wind speed is negative.");
                }
			}
			else if (reader.IsStartElement("speedMax"))
			{
				this.SpeedMax = reader.ReadElementContentAsDouble();
				if (this.SpeedMax < 0.0)
				{
					throw new XmlException("Maximum wind speed is negative.");
				}
			}
			else if (reader.IsStartElement("direction"))
			{
				this.Direction = reader.ReadElementContentAsDouble();
				if ((this.Direction < 0.0) || (this.Direction > 360.0))
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
