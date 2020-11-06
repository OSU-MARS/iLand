using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class BarkBeetleReferenceClimate : XmlSerializable
    {
		public string TableName { get; private set; }
		public string SeasonalPrecipSum { get; private set; }
		public string SeasonalTemperatureAverage { get; private set; }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("referenceClimate"))
			{
				reader.Read();
				return;
			}
			else if (reader.IsStartElement("tableName"))
			{
				this.TableName = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("seasonalPrecipSum"))
			{
				this.SeasonalPrecipSum = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("seasonalTemperatureAverage"))
			{
				this.SeasonalTemperatureAverage = reader.ReadElementContentAsString().Trim();
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
