using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class BarkBeetle : Enablable
	{
		public double MinimumDbh { get; private set; }
		public double BackgroundInfestationProbability { get; private set; }
		public double StormInfestationProbability { get; private set; }
		public double BaseWinterMortality { get; private set; }
		public string WinterMortalityFormula { get; private set; }
		public string SpreadKernelFormula { get; private set; }
		public double SpreadKernelMaxDistance { get; private set; }
		public int CohortsPerGeneration { get; private set; }
		public int CohortsPerSisterbrood { get; private set; }
		public string ColonizeProbabilityFormula { get; private set; }
		public double DeadTreeSelectivity { get; private set; }
		public string OutbreakClimateSensitivityFormula { get; private set; }
		public int OutbreakDurationMin { get; private set; }
		public int OutbreakDurationMax { get; private set; }
		public string OutbreakDurationMortalityFormula { get; private set; }
		public double InitialInfestationProbability { get; private set; }
		public BarkBeetleReferenceClimate ReferenceClimate { get; private set; }
		public string OnAfterBarkbeetle { get; private set; }

		public BarkBeetle()
        {
			this.ReferenceClimate = new BarkBeetleReferenceClimate();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("barkbeetle"))
			{
				reader.Read();
				return;
			}
			else if (reader.IsStartElement("enabled"))
			{
				this.Enabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("minimumDbh"))
			{
				this.MinimumDbh = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("backgroundInfestationProbability"))
			{
				this.BackgroundInfestationProbability = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("stormInfestationProbability"))
			{
				this.StormInfestationProbability = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("baseWinterMortality"))
			{
				this.BaseWinterMortality = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("winterMortalityFormula"))
			{
				this.WinterMortalityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("spreadKernelFormula"))
			{
				this.SpreadKernelFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("spreadKernelMaxDistance"))
			{
				this.SpreadKernelMaxDistance = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("cohortsPerGeneration"))
			{
				this.CohortsPerGeneration = reader.ReadElementContentAsInt();
			}
			else if (reader.IsStartElement("cohortsPerSisterbrood"))
			{
				this.CohortsPerSisterbrood = reader.ReadElementContentAsInt();
			}
			else if (reader.IsStartElement("colonizeProbabilityFormula"))
			{
				this.ColonizeProbabilityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("deadTreeSelectivity"))
			{
				this.DeadTreeSelectivity = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("outbreakClimateSensitivityFormula"))
			{
				this.OutbreakClimateSensitivityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("outbreakDurationMin"))
			{
				this.OutbreakDurationMin = reader.ReadElementContentAsInt();
			}
			else if (reader.IsStartElement("outbreakDurationMax"))
			{
				this.OutbreakDurationMax = reader.ReadElementContentAsInt();
			}
			else if (reader.IsStartElement("outbreakDurationMortalityFormula"))
			{
				this.OutbreakDurationMortalityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("initialInfestationProbability"))
			{
				this.InitialInfestationProbability = reader.ReadElementContentAsDouble();
			}
			else if (reader.IsStartElement("referenceClimate"))
			{
				this.ReferenceClimate.ReadXml(reader);
			}
			else if (reader.IsStartElement("onAfterBarkbeetle"))
			{
				this.OnAfterBarkbeetle = reader.ReadElementContentAsString().Trim();
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
