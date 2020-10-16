using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class SeedBeltSpecies
    {
        // space separated list of four letter species codes
        [XmlText]
        public string IDs { get; set; }

        [XmlAttribute(AttributeName = "x")]
        public int X { get; set; }

        [XmlAttribute(AttributeName = "y")]
        public int Y { get; set; }
    }
}
