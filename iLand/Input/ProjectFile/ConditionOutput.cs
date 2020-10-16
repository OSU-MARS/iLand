using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class FilterOutput : Enablable
    {
        [XmlElement(ElementName = "filter")]
        public string Filter { get; set; }

        public FilterOutput()
        {
            this.Filter = null;
        }
    }
}
