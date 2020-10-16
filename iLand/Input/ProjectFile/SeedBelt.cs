using System.Collections.Generic;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class SeedBelt : Enablable
    {
        [XmlElement(ElementName = "width")]
        public int Width { get; set; }

		[XmlElement(ElementName = "sizeX")]
		public int SizeX { get; set; }

		[XmlElement(ElementName = "sizeY")]
		public int SizeY { get; set; }

		[XmlElement(ElementName = "species")]
		public List<SeedBeltSpecies> Species { get; set; }

		public SeedBelt()
        {
			this.Width = 10;
			this.SizeX = 0;
			this.SizeY = 0;

			this.Species = new List<SeedBeltSpecies>();
        }
    }
}
