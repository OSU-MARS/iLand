using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Climate : XmlSerializable
    {
        public float CO2ConcentrationInPpm { get; private set; }
        public string TableName { get; private set; }
        public int BatchYears { get; private set; }
        public float TemperatureShift { get; private set; }
        public float PrecipitationMultiplier { get; private set; }
        public bool RandomSamplingEnabled { get; private set; }
        public string RandomSamplingList { get; private set; }
        public string Filter { get; private set; }

        public Climate()
        {
            // no default in C++
            // this.Filter;

            this.BatchYears = 100;
            this.CO2ConcentrationInPpm = 400.0F; 
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

            if (reader.IsStartElement("climate"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("co2concentration"))
            {
                this.CO2ConcentrationInPpm = reader.ReadElementContentAsFloat();
                if ((this.CO2ConcentrationInPpm < 0.0F) || (this.CO2ConcentrationInPpm > 1E6F))
                {
                    throw new XmlException("CO₂ concentration is negative or greater than 100%.");
                }
            }
            else if (reader.IsStartElement("tableName"))
            {
                this.TableName = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("batchYears"))
            {
                this.BatchYears = reader.ReadElementContentAsInt();
                if (this.BatchYears < 1)
                {
                    throw new XmlException("Maximum number of years to load per climate table read (batchYears) is zero or negative.");
                }
            }
            else if (reader.IsStartElement("temperatureShift"))
            {
                this.TemperatureShift = reader.ReadElementContentAsFloat();
                // no restriction on value range
            }
            else if (reader.IsStartElement("precipitationShift"))
            {
                this.PrecipitationMultiplier = reader.ReadElementContentAsFloat();
                // no restriction on value range
            }
            else if (reader.IsStartElement("randomSamplingEnabled"))
            {
                this.RandomSamplingEnabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("randomSamplingList"))
            {
                this.RandomSamplingList = reader.ReadElementContentAsString().Trim();
            }
            else if (reader.IsStartElement("filter"))
            {
                this.Filter = reader.ReadElementContentAsString().Trim();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
