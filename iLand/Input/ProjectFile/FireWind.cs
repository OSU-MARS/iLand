using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class FireWind
    {
        [XmlElement(ElementName = "speedMin")]
        public double SpeedMin { get; set; }

        [XmlElement(ElementName = "speedMax")]
        public double SpeedMax { get; set; }

        [XmlElement(ElementName = "direction")]
        public double Direction { get; set; }
    }
}
