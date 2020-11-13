using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class BarkBeetle : Enablable
	{
		public float MinimumDbh { get; private set; }
		public float BackgroundInfestationProbability { get; private set; }
		public float StormInfestationProbability { get; private set; }
		public float BaseWinterMortality { get; private set; }
		public string? WinterMortalityFormula { get; private set; }
		public string? SpreadKernelFormula { get; private set; }
		public float SpreadKernelMaxDistance { get; private set; }
		public int CohortsPerGeneration { get; private set; }
		public int CohortsPerSisterbrood { get; private set; }
		public string? ColonizeProbabilityFormula { get; private set; }
		public float DeadTreeSelectivity { get; private set; }
		public string? OutbreakClimateSensitivityFormula { get; private set; }
		public int OutbreakDurationMin { get; private set; }
		public int OutbreakDurationMax { get; private set; }
		public string? OutbreakDurationMortalityFormula { get; private set; }
		public float InitialInfestationProbability { get; private set; }
		public BarkBeetleReferenceClimate ReferenceClimate { get; private set; }
		public string? OnAfterBarkbeetle { get; private set; }

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
				this.MinimumDbh = reader.ReadElementContentAsFloat();
				if (this.MinimumDbh < 0.0F)
                {
					throw new XmlException("Minimum DBH is negative.");
                }
			}
			else if (reader.IsStartElement("backgroundInfestationProbability"))
			{
				this.BackgroundInfestationProbability = reader.ReadElementContentAsFloat();
				if (this.MinimumDbh < 0.0F)
				{
					throw new XmlException("Minimum DBH is negative.");
				}
			}
			else if (reader.IsStartElement("stormInfestationProbability"))
			{
				this.StormInfestationProbability = reader.ReadElementContentAsFloat();
				if ((this.StormInfestationProbability < 0.0F) || (this.StormInfestationProbability > 1.0F))
				{
					throw new XmlException("Storm infestationprobability is negative or greater than 1.0.");
				}
			}
			else if (reader.IsStartElement("baseWinterMortality"))
			{
				this.BaseWinterMortality = reader.ReadElementContentAsFloat();
				if ((this.BaseWinterMortality < 0.0F) || (this.BaseWinterMortality > 1.0F))
				{
					throw new XmlException("Base probability of winter mortality is negative or greater than 1.0.");
				}
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
				this.SpreadKernelMaxDistance = reader.ReadElementContentAsFloat();
				if (this.SpreadKernelMaxDistance < 0.0F)
                {
					throw new XmlException("Maximum distance beetles can fly (spreadKernelMaxDistance) is negative.");
                }
			}
			else if (reader.IsStartElement("cohortsPerGeneration"))
			{
				this.CohortsPerGeneration = reader.ReadElementContentAsInt();
				if (this.CohortsPerGeneration < 0)
				{
					throw new XmlException("Cohorts per generation is negative.");
				}
			}
			else if (reader.IsStartElement("cohortsPerSisterbrood"))
			{
				this.CohortsPerSisterbrood = reader.ReadElementContentAsInt();
				if (this.CohortsPerSisterbrood < 0)
				{
					throw new XmlException("Cohorts per sister brood is negative.");
				}
			}
			else if (reader.IsStartElement("colonizeProbabilityFormula"))
			{
				this.ColonizeProbabilityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("deadTreeSelectivity"))
			{
				this.DeadTreeSelectivity = reader.ReadElementContentAsFloat();
				if ((this.DeadTreeSelectivity < 0.0F) || (this.DeadTreeSelectivity > 1.0F))
				{
					throw new XmlException("Dead tree selectivity is negative or greater than 1.0.");
				}
			}
			else if (reader.IsStartElement("outbreakClimateSensitivityFormula"))
			{
				this.OutbreakClimateSensitivityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("outbreakDurationMin"))
			{
				this.OutbreakDurationMin = reader.ReadElementContentAsInt();
				if (this.OutbreakDurationMin < 0)
				{
					throw new XmlException("Minimum outbreak duration is negative.");
				}
			}
			else if (reader.IsStartElement("outbreakDurationMax"))
			{
				this.OutbreakDurationMax = reader.ReadElementContentAsInt();
				if (this.OutbreakDurationMax < 0)
				{
					throw new XmlException("Maximum outbreak duration is negative.");
				}
			}
			else if (reader.IsStartElement("outbreakDurationMortalityFormula"))
			{
				this.OutbreakDurationMortalityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("initialInfestationProbability"))
			{
				this.InitialInfestationProbability = reader.ReadElementContentAsFloat();
				if ((this.InitialInfestationProbability < 0.0F) || (this.InitialInfestationProbability > 1.0F))
				{
					throw new XmlException("Initial infestation probability is negative or greater than 1.0.");
				}
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
