using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Climate
    {
        [XmlElement(ElementName = "co2concentration")]
        public double CO2Concentration { get; set; }

        [XmlElement(ElementName = "tableName")]
        public string TableName { get; set; }

        [XmlElement(ElementName = "batchYears")]
        public int BatchYears { get; set; }

        [XmlElement(ElementName = "temperatureShift")]
        public float TemperatureShift { get; set; }

        [XmlElement(ElementName = "precipitationShift")]
        public float PrecipitationMultiplier { get; set; }

        [XmlElement(ElementName = "randomSamplingEnabled")]
        public bool RandomSamplingEnabled { get; set; }

        [XmlElement(ElementName = "randomSamplingList")]
        public string RandomSamplingList { get; set; }

        [XmlElement(ElementName = "filter")]
        public string Filter { get; set; }

        public Climate()
        {
            // no default in C++
            // this.Filter;

            this.BatchYears = 100;
            this.CO2Concentration = 400.0;
            this.PrecipitationMultiplier = 1.0F;
            this.RandomSamplingEnabled = false;
            this.RandomSamplingList = null;
            this.TemperatureShift = 0.0F;
        }
    }
}
