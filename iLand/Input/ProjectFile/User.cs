using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class User
    {
        [XmlElement(ElementName = "code")]
        public string Code { get; set; }

        [XmlElement(ElementName = "windspeed_factor")]
        public double WindspeedFactor { get; set; }
    }
}
