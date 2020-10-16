using iLand.Output;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class DynamicStandOutput : ConditionOutput
    {
        [XmlElement(ElementName = "rufilter")]
        public string RUFilter { get; set; }

        [XmlElement(ElementName = "treefilter")]
        public string TreeFilter { get; set; }

        [XmlElement(ElementName = "by_species")]
        public bool BySpecies { get; set; }

        [XmlElement(ElementName = "by_ru")]
        public bool ByResourceUnit { get; set; }

        [XmlElement(ElementName = "columns")]
        public string Columns { get; set; }

        public DynamicStandOutput()
        {
            this.ByResourceUnit = true;
            this.BySpecies = true;
            this.Columns = null;
            this.RUFilter = null;
            this.TreeFilter = null;
        }
    }
}
