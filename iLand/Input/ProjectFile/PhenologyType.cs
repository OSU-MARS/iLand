using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class PhenologyType : XmlSerializable
    {
        public int ID { get; private set; }
        public float VpdMin { get; private set; }
        public float VpdMax { get; private set; }
        public float DayLengthMin { get; private set; }
        public float DayLengthMax { get; private set; }
        public float TempMin { get; private set; }
        public float TempMax { get; private set; }

        public PhenologyType()
        {
            // no default in C++
            // this.ID;

            this.VpdMax = 5.0F;
            this.VpdMin = 0.5F;
            this.DayLengthMax = 11.0F;
            this.DayLengthMin = 10.0F;
            this.TempMax = 9.0F;
            this.TempMin = 2.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                if (String.Equals(reader.Name, "type", StringComparison.Ordinal))
                {
                    if (reader.AttributeCount != 1)
                    {
                        throw new XmlException("Encountered unexpected attributes.");
                    }

                    string? idAsString = reader.GetAttribute("id");
                    if (String.IsNullOrWhiteSpace(idAsString))
                    {
                        throw new XmlException("id attribute of phenology type is empty.");
                    }
                    this.ID = Int32.Parse(idAsString);
                    reader.ReadStartElement();
                }
                else
                {
                    throw new XmlException("Encountered unexpected attributes.");
                }
            }
            else if (String.Equals(reader.Name, "vpdMin", StringComparison.Ordinal))
            {
                this.VpdMin = reader.ReadElementContentAsFloat();
                if (this.VpdMin < 0.0F)
                {
                    throw new XmlException("Minimum vapor pressure deficit is negative.");
                }
            }
            else if (String.Equals(reader.Name, "vpdMax", StringComparison.Ordinal))
            {
                this.VpdMax = reader.ReadElementContentAsFloat();
                if (this.VpdMax < 0.0F)
                {
                    throw new XmlException("Minimum vapor pressure deficit is negative.");
                }
            }
            else if (String.Equals(reader.Name, "dayLengthMin", StringComparison.Ordinal))
            {
                this.DayLengthMin = reader.ReadElementContentAsFloat();
                if (this.DayLengthMin < 0.0F)
                {
                    throw new XmlException("Minimum day length is negative.");
                }
            }
            else if (String.Equals(reader.Name, "dayLengthMax", StringComparison.Ordinal))
            {
                this.DayLengthMax = reader.ReadElementContentAsFloat();
                if (this.DayLengthMax < 0.0F)
                {
                    throw new XmlException("Maximum day length is negative.");
                }
            }
            else if (String.Equals(reader.Name, "tempMin", StringComparison.Ordinal))
            {
                this.TempMin = reader.ReadElementContentAsFloat();
                if (this.TempMin < -273.15F)
                {
                    throw new XmlException("Minimum temperature is below absolute zero.");
                }
            }
            else if (String.Equals(reader.Name, "tempMax", StringComparison.Ordinal))
            {
                this.TempMax = reader.ReadElementContentAsFloat();
                if (this.TempMax < -273.15F)
                {
                    throw new XmlException("Maximum temperature is below absolute zero.");
                }
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
