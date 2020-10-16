using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class LandscapeRemovedOutput : Enablable
    {
		[XmlElement(ElementName = "includeHarvest")]
		public bool IncludeHarvest { get; set; }

		[XmlElement(ElementName = "includeNatural")]
		public bool IncludeNatural { get; set; }

		public LandscapeRemovedOutput()
        {
            this.IncludeHarvest = true;
            this.IncludeNatural = false;
        }
    }
}
