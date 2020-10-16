using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class NitrogenResponseClasses
    {
        [XmlElement(ElementName = "class_1_a")]
        public double Class1A { get; set; }

        [XmlElement(ElementName = "class_1_b")]
        public double Class1B { get; set; }

        [XmlElement(ElementName = "class_2_a")]
        public double Class2A { get; set; }

        [XmlElement(ElementName = "class_2_b")]
        public double Class2B { get; set; }

        [XmlElement(ElementName = "class_3_a")]
        public double Class3A { get; set; }

        [XmlElement(ElementName = "class_3_b")]
        public double Class3B { get; set; }

        public NitrogenResponseClasses()
        {
            // no defaults in C++, must be set in project file
            this.Class1A = 0.0;
            this.Class1B = 0.0;
            this.Class2A = 0.0;
            this.Class2B = 0.0;
            this.Class3A = 0.0;
            this.Class3B = 0.0;
        }
    }
}
