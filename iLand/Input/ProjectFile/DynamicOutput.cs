using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class DynamicOutput : ConditionOutput
    {
        [XmlElement(ElementName = "columns")]
        public string Columns { get; set; }

        [XmlElement(ElementName = "rufilter")]
        public string RUFilter { get; set; }

        [XmlElement(ElementName = "treefilter")]
        public string TreeFilter { get; set; }

        public DynamicOutput()
        {
            this.Columns = null;
            this.RUFilter = null;
            this.TreeFilter = null;
        }
    }
}
