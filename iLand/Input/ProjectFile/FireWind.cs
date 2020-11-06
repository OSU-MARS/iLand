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
			}
			else if (reader.IsStartElement("speedMax"))
			{
				this.SpeedMax = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("direction"))
			{
				this.Direction = reader.ReadElementContentAsDouble();
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
