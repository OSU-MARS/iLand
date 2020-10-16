using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class BarkBeetle : Enablable
	{
		[XmlElement(ElementName = "minimumDbh")]
		public double MinimumDbh { get; set; }

		[XmlElement(ElementName = "backgroundInfestationProbability")]
		public double BackgroundInfestationProbability { get; set; }

		[XmlElement(ElementName = "stormInfestationProbability")]
		public double StormInfestationProbability { get; set; }

		[XmlElement(ElementName = "baseWinterMortality")]
		public double BaseWinterMortality { get; set; }

		[XmlElement(ElementName = "winterMortalityFormula")]
		public string WinterMortalityFormula { get; set; }

		[XmlElement(ElementName = "spreadKernelFormula")]
		public string SpreadKernelFormula { get; set; }

		[XmlElement(ElementName = "spreadKernelMaxDistance")]
		public double SpreadKernelMaxDistance { get; set; }

		[XmlElement(ElementName = "cohortsPerGeneration")]
		public int CohortsPerGeneration { get; set; }

		[XmlElement(ElementName = "cohortsPerSisterbrood")]
		public int CohortsPerSisterbrood { get; set; }

		[XmlElement(ElementName = "colonizeProbabilityFormula")]
		public string ColonizeProbabilityFormula { get; set; }

		[XmlElement(ElementName = "deadTreeSelectivity")]
		public double DeadTreeSelectivity { get; set; }

		[XmlElement(ElementName = "outbreakClimateSensitivityFormula")]
		public string OutbreakClimateSensitivityFormula { get; set; }

		[XmlElement(ElementName = "outbreakDurationMin")]
		public int OutbreakDurationMin { get; set; }

		[XmlElement(ElementName = "outbreakDurationMax")]
		public int OutbreakDurationMax { get; set; }

		[XmlElement(ElementName = "outbreakDurationMortalityFormula")]
		public string OutbreakDurationMortalityFormula { get; set; }

		[XmlElement(ElementName = "initialInfestationProbability")]
		public double InitialInfestationProbability { get; set; }

		[XmlElement(ElementName = "referenceClimate")]
		public BarkBeetleReferenceClimate ReferenceClimate { get; set; }

		[XmlElement(ElementName = "onAfterBarkbeetle")]
		public string OnAfterBarkbeetle { get; set; }
    }
}
