using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class StandGrid : Enablable
    {
        [XmlElement(ElementName = "fileName")]
        public string FileName { get; set; }

        public StandGrid()
        {
            this.FileName = null;
        }
    }
}
