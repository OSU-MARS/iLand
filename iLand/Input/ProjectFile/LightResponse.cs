using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class LightResponse
    {
        [XmlElement(ElementName = "shadeIntolerant")]
        public string ShadeIntolerant { get; set; }

        [XmlElement(ElementName = "shadeTolerant")]
        public string ShadeTolerant { get; set; }

        [XmlElement(ElementName = "LRImodifier")]
        public string LriModifier { get; set; }

        public LightResponse()
        {
            // no defaults in C++, must be set in project file
            this.ShadeIntolerant = null;
            this.ShadeTolerant = null;

            this.LriModifier = "1";
        }
    }
}
