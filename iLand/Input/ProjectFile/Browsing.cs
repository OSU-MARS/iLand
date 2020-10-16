using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Browsing : Enablable
    {
        [XmlElement(ElementName = "browsingPressure")]
        public double BrowsingPressure { get; set; }

        public Browsing()
        {
            this.BrowsingPressure = 0.0;
        }
    }
}
