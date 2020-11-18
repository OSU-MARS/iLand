using System;
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
            this.PrecipitationMultiplier = 1.0F;
            this.RandomSamplingEnabled = false;
            this.RandomSamplingList = null;
            this.TemperatureShift = 0.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "climate", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "co2concentration", StringComparison.Ordinal))
            {
                this.CO2ConcentrationInPpm = reader.ReadElementContentAsFloat();
                if ((this.CO2ConcentrationInPpm < 0.0F) || (this.CO2ConcentrationInPpm > 1E6F))
                {
                    throw new XmlException("CO₂ concentration is negative or greater than 100%.");
                }
            }
            else if (String.Equals(reader.Name, "databaseFile", StringComparison.Ordinal))
            {
                this.DatabaseFile = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "databaseQueryFilter", StringComparison.Ordinal))
            {
                this.DatabaseQueryFilter = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "defaultDatabaseTable", StringComparison.Ordinal))
            {
                this.DefaultDatabaseTable = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "batchYears", StringComparison.Ordinal))
            {
                this.BatchYears = reader.ReadElementContentAsInt();
                if (this.BatchYears < 1)
                {
                    throw new XmlException("Maximum number of years to load per climate table read (batchYears) is zero or negative.");
                }
            }
            else if (String.Equals(reader.Name, "temperatureShift", StringComparison.Ordinal))
            {
                this.TemperatureShift = reader.ReadElementContentAsFloat();
                // no restriction on value range
            }
            else if (String.Equals(reader.Name, "precipitationShift", StringComparison.Ordinal))
            {
                this.PrecipitationMultiplier = reader.ReadElementContentAsFloat();
                // no restriction on value range
            }
            else if (String.Equals(reader.Name, "randomSamplingEnabled", StringComparison.Ordinal))
            {
                this.RandomSamplingEnabled = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "randomSamplingList", StringComparison.Ordinal))
            {
                this.RandomSamplingList = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
