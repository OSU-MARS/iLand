using System;
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
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (String.Equals(reader.Name, "referenceClimate", StringComparison.Ordinal))
			{
				reader.Read();
				return;
			}
			else if (String.Equals(reader.Name, "tableName", StringComparison.Ordinal))
			{
				this.TableName = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "seasonalPrecipSum", StringComparison.Ordinal))
			{
				this.SeasonalPrecipSum = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "seasonalTemperatureAverage", StringComparison.Ordinal))
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
