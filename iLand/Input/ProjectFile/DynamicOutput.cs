using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class DynamicOutput : ResourceUnitFilterOutput
    {
        [XmlElement(ElementName = "columns")]
        public string Columns { get; set; }

        [XmlElement(ElementName = "treefilter")]
        public string TreeFilter { get; set; }

        public DynamicOutput()
        {
            this.Columns = null;
            this.ResourceUnitFilter = null;
            this.TreeFilter = null;
        }
    }
}
