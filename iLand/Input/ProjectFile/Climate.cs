using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Climate : XmlSerializable
    {
        public int BatchYears { get; private set; }
        public float CO2ConcentrationInPpm { get; private set; }
        public string? DatabaseFile { get; private set; }
        public string? DatabaseQueryFilter { get; private set; }
        public string? DefaultDatabaseTable { get; private set; }
        public float PrecipitationMultiplier { get; private set; }
        public bool RandomSamplingEnabled { get; private set; }
        public string? RandomSamplingList { get; private set; }
        public float TemperatureShift { get; private set; }

        public Climate()
        {
            // no default in C++
            // this.Filter;

            this.BatchYears = 100;
            this.CO2ConcentrationInPpm = 400.0F;
            this.DatabaseFile = null;
            this.DatabaseQueryFilter = null;
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
                case "climate":
                    reader.Read();
                    break;
                case "co2concentration":
                    this.CO2ConcentrationInPpm = reader.ReadElementContentAsFloat();
                    if ((this.CO2ConcentrationInPpm < 0.0F) || (this.CO2ConcentrationInPpm > 1E6F))
                    {
                        throw new XmlException("CO₂ concentration is negative or greater than 100%.");
                    }
                    break;
                case "databaseFile":
                    this.DatabaseFile = reader.ReadElementContentAsString().Trim();
                    break;
                case "databaseQueryFilter":
                    this.DatabaseQueryFilter = reader.ReadElementContentAsString().Trim();
                    break;
                case "defaultDatabaseTable":
                    this.DefaultDatabaseTable = reader.ReadElementContentAsString().Trim();
                    break;
                case "batchYears":
                    this.BatchYears = reader.ReadElementContentAsInt();
                    if (this.BatchYears < 1)
                    {
                        throw new XmlException("Maximum number of years to load per climate table read (batchYears) is zero or negative.");
                    }
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
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
