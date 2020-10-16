using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class PhenologyType
    {
        [XmlAttribute(AttributeName = "id")]
        public int ID { get; set; }

        [XmlElement(ElementName = "vpdMin")]
        public double VpdMin { get; set; }

        [XmlElement(ElementName = "vpdMax")]
        public double VpdMax { get; set; }

        [XmlElement(ElementName = "dayLengthMin")]
        public double DayLengthMin { get; set; }

        [XmlElement(ElementName = "dayLengthMax")]
        public double DayLengthMax { get; set; }

        [XmlElement(ElementName = "tempMin")]
        public double TempMin { get; set; }

        [XmlElement(ElementName = "tempMax")]
        public double TempMax { get; set; }

        public PhenologyType()
        {
            // no default in C++
            // this.ID;

            this.VpdMax = 5.0;
            this.VpdMin = 0.5;
            this.DayLengthMax = 11.0;
            this.DayLengthMin = 10.0;
            this.TempMax = 9.0;
            this.TempMin = 2.0;
        }
    }
}
