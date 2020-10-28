using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class DynamicStandOutput : ResourceUnitFilterOutput
    {
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
            this.ResourceUnitFilter = null;
            this.TreeFilter = null;
        }
    }
}
