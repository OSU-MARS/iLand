using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Weather : XmlSerializable
    {
        public int BatchYears { get; private set; }
        public float CO2ConcentrationInPpm { get; private set; }
        public string? File { get; private set; }
        public string? DatabaseQueryFilter { get; private set; }
        public string? DefaultDatabaseTable { get; private set; }
        public float PrecipitationMultiplier { get; private set; }
        public bool RandomSamplingEnabled { get; private set; }
        public string? RandomSamplingList { get; private set; }
        public float TemperatureShift { get; private set; }

        public Weather()
        {
            this.BatchYears = 100;
            this.CO2ConcentrationInPpm = 400.0F;
            this.File = null;
            this.DatabaseQueryFilter = null; // no default in C++
            this.DefaultDatabaseTable = null;
            this.PrecipitationMultiplier = 1.0F;
            this.RandomSamplingEnabled = false;
            this.RandomSamplingList = null;
            this.TemperatureShift = 0.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "batchYears":
                    this.BatchYears = reader.ReadElementContentAsInt();
                    if (this.BatchYears < 1)
                    {
                        throw new XmlException("Maximum number of years to load per daily weather table read (batchYears) is zero or negative.");
                    }
                    break;
                case "co2concentration":
                    this.CO2ConcentrationInPpm = reader.ReadElementContentAsFloat();
                    if ((this.CO2ConcentrationInPpm < 0.0F) || (this.CO2ConcentrationInPpm > 1E6F))
                    {
                        throw new XmlException("CO₂ concentration is negative or greater than 100%.");
                    }
                    break;
                case "databaseQueryFilter":
                    this.DatabaseQueryFilter = reader.ReadElementContentAsString().Trim();
                    break;
                case "defaultDatabaseTable":
                    this.DefaultDatabaseTable = reader.ReadElementContentAsString().Trim();
                    break;
                case "file":
                    this.File = reader.ReadElementContentAsString().Trim();
                    break;
                case "temperatureShift":
                    this.TemperatureShift = reader.ReadElementContentAsFloat();
                    // no restriction on value range
                    break;
                case "precipitationShift":
                    this.PrecipitationMultiplier = reader.ReadElementContentAsFloat();
                    // no restriction on value range
                    break;
                case "randomSamplingEnabled":
                    this.RandomSamplingEnabled = reader.ReadElementContentAsBoolean();
                    break;
                case "randomSamplingList":
                    this.RandomSamplingList = reader.ReadElementContentAsString().Trim();
                    break;
                case "weather":
                    reader.Read();
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
