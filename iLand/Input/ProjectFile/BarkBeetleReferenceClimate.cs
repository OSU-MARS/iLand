using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class BarkBeetleReferenceClimate : XmlSerializable
    {
		public string? TableName { get; private set; }
		public string? SeasonalPrecipSum { get; private set; }
		public string? SeasonalTemperatureAverage { get; private set; }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			switch (reader.Name)
			{
				case "referenceClimate":
					reader.Read();
					break;
				case "tableName":
					this.TableName = reader.ReadElementContentAsString().Trim();
					break;
				case "seasonalPrecipSum":
					this.SeasonalPrecipSum = reader.ReadElementContentAsString().Trim();
					break;
				case "seasonalTemperatureAverage":
					this.SeasonalTemperatureAverage = reader.ReadElementContentAsString().Trim();
					break;
				default:
					throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
