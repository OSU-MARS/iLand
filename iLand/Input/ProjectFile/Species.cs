using System.Collections.Generic;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Species
    {
        [XmlElement(ElementName = "source")]
        public string Source { get; set; }

        [XmlElement(ElementName = "reader")]
        public string ReaderStampFile { get; set; }

        [XmlElement(ElementName = "nitrogenResponseClasses")]
        public NitrogenResponseClasses NitrogenResponseClasses { get; set; }

        [XmlElement(ElementName = "CO2Response")]
        public CO2Response CO2Response { get; set; }

        [XmlElement(ElementName = "lightResponse")]
        public LightResponse LightResponse { get; set; }

        [XmlArray(ElementName = "phenology")]
        [XmlArrayItem(ElementName = "type")]
        public List<PhenologyType> Phenology { get; set; }

        public Species()
        {
            this.CO2Response = new CO2Response();
            this.LightResponse = new LightResponse();
            this.NitrogenResponseClasses = new NitrogenResponseClasses();
            this.ReaderStampFile = "readerstamp.bin";
            // this.Source;

            this.Phenology = new List<PhenologyType>();
        }
    }
}
