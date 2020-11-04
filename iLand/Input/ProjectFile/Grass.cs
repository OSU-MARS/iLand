using iLand.World;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Grass : Enablable
    {
		[XmlElement(ElementName = "type")]
		public GrassAlgorithmType Type { get; set; }

		[XmlElement(ElementName = "grassDuration")]
		public string GrassDuration { get; set; }

		[XmlElement(ElementName = "LIFThreshold")]
		public float LifThreshold { get; set; }

		[XmlElement(ElementName = "grassPotential")]
		public string GrassPotential { get; set; }

		[XmlElement(ElementName = "maxTimeLag")]
		public int MaxTimeLag { get; set; }

		[XmlElement(ElementName = "grassEffect")]
		public string GrassEffect { get; set; }

		public Grass()
        {
			this.GrassDuration = null;
			this.GrassPotential = null;
			this.LifThreshold = 0.2F;
			this.MaxTimeLag = 0; // TODO: unhelpful default since minimum value is 1
			this.Type = GrassAlgorithmType.Invalid; // TODO: unhelpful default, C++ defaulted to null string
        }
    }
}
