using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class HeightGrid : Enablable
    {
		[XmlElement(ElementName = "fileName")]
		public string FileName { get; set; }

		[XmlElement(ElementName = "maxTries")]
		public int MaxTries { get; set; }

		[XmlElement(ElementName = "fitFormula")]
		public string FitFormula { get; set; }

		public HeightGrid()
        {
			this.FileName = "init";
			this.FitFormula = "polygon(x, 0,0, 0.8,1, 1.1, 1, 1.25,0)";
			this.MaxTries = 10;
        }
    }
}
