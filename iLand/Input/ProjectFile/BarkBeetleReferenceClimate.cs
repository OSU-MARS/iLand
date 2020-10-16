using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class BarkBeetleReferenceClimate
    {
		[XmlElement(ElementName = "tableName")]
		public string TableName { get; set; }

		[XmlElement(ElementName = "seasonalPrecipSum")]
		public string SeasonalPrecipSum { get; set; }

		[XmlElement(ElementName = "seasonalTemperatureAverage")]
		public string SeasonalTemperatureAverage { get; set; }
    }
}
