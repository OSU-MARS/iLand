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
                if (reader.IsStartElement("type"))
                {
                    if (reader.AttributeCount != 1)
                    {
                        throw new XmlException("Encountered unexpected attributes.");
                    }

                    this.ID = Int32.Parse(reader.GetAttribute("id"));
                    reader.ReadStartElement();
                }
                else
                {
                    throw new XmlException("Encountered unexpected attributes.");
                }
            }
            else if (reader.IsStartElement("vpdMin"))
            {
                this.VpdMin = reader.ReadElementContentAsFloat();
                if (this.VpdMin < 0.0F)
                {
                    throw new XmlException("Minimum vapor pressure deficit is negative.");
                }
            }
            else if (reader.IsStartElement("vpdMax"))
            {
                this.VpdMax = reader.ReadElementContentAsFloat();
                if (this.VpdMax < 0.0F)
                {
                    throw new XmlException("Minimum vapor pressure deficit is negative.");
                }
            }
            else if (reader.IsStartElement("dayLengthMin"))
            {
                this.DayLengthMin = reader.ReadElementContentAsFloat();
                if (this.DayLengthMin < 0.0F)
                {
                    throw new XmlException("Minimum day length is negative.");
                }
            }
            else if (reader.IsStartElement("dayLengthMax"))
            {
                this.DayLengthMax = reader.ReadElementContentAsFloat();
                if (this.DayLengthMax < 0.0F)
                {
                    throw new XmlException("Maximum day length is negative.");
                }
            }
            else if (reader.IsStartElement("tempMin"))
            {
                this.TempMin = reader.ReadElementContentAsFloat();
                if (this.TempMin < -273.15F)
                {
                    throw new XmlException("Minimum temperature is below absolute zero.");
                }
            }
            else if (reader.IsStartElement("tempMax"))
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
