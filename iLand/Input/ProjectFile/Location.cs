using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Location
    {
        [XmlElement(ElementName = "x")]
        public double X { get; set; }

        [XmlElement(ElementName = "y")]
        public double Y { get; set; }

        [XmlElement(ElementName = "z")]
        public double Z { get; set; }

        [XmlElement(ElementName = "rotation")]
        public double Rotation { get; set; }

        public Location()
        {
            this.X = 0.0;
            this.Y = 0.0;
            this.Z = 0.0;
            this.Rotation = 0.0;
        }
    }
}
