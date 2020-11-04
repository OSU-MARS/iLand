using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class PhenologyType
    {
        [XmlAttribute(AttributeName = "id")]
        public int ID { get; set; }

        [XmlElement(ElementName = "vpdMin")]
        public float VpdMin { get; set; }

        [XmlElement(ElementName = "vpdMax")]
        public float VpdMax { get; set; }

        [XmlElement(ElementName = "dayLengthMin")]
        public float DayLengthMin { get; set; }

        [XmlElement(ElementName = "dayLengthMax")]
        public float DayLengthMax { get; set; }

        [XmlElement(ElementName = "tempMin")]
        public float TempMin { get; set; }

        [XmlElement(ElementName = "tempMax")]
        public float TempMax { get; set; }

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
    }
}
