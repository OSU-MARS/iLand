using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Weather : XmlSerializable
    {
        public string? CO2File { get; private set; }
        public int DailyWeatherChunkSizeInYears { get; private set; }
        public string? DefaultDatabaseTable { get; private set; }
        public float PrecipitationMultiplier { get; private set; }
        public bool RandomSamplingEnabled { get; private set; }
        public string? RandomSamplingList { get; private set; }
        public Int16 StartYear { get; private set; }
        public float TemperatureShift { get; private set; }
        public string? WeatherFile { get; private set; }

        public Weather()
        {
            this.CO2File = null;
            this.DailyWeatherChunkSizeInYears = 100;
            this.DefaultDatabaseTable = null;
            this.PrecipitationMultiplier = 1.0F;
            this.RandomSamplingEnabled = false;
            this.RandomSamplingList = null;
            this.StartYear = Int16.MinValue; // min value so all years are included by default (no SQL query string default in C++)
            this.TemperatureShift = 0.0F;
            this.WeatherFile = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "co2file":
                    this.CO2File = reader.ReadElementContentAsString().Trim();
                    break;
                case "dailyWeatherChunkSizeInYears":
                    this.DailyWeatherChunkSizeInYears = reader.ReadElementContentAsInt();
                    if (this.DailyWeatherChunkSizeInYears < 1)
                    {
                        throw new XmlException("Maximum number of years to load per daily weather table read (batchYears) is zero or negative.");
                    }
                    break;
                case "defaultDatabaseTable":
                    this.DefaultDatabaseTable = reader.ReadElementContentAsString().Trim();
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
                case "startYear":
                    this.StartYear = (Int16)reader.ReadElementContentAsInt();
                    break;
                case "weather":
                    reader.Read();
                    break;
                case "weatherFile":
                    this.WeatherFile = reader.ReadElementContentAsString().Trim();
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
