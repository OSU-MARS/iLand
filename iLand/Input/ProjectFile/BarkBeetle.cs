using System;
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
		public BarkBeetleReferenceClimate ReferenceClimate { get; init; }
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

			if (String.Equals(reader.Name, "barkbeetle", StringComparison.Ordinal))
			{
				reader.Read();
				return;
			}
			else if (String.Equals(reader.Name, "enabled", StringComparison.Ordinal))
			{
				this.Enabled = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "minimumDbh", StringComparison.Ordinal))
			{
				this.MinimumDbh = reader.ReadElementContentAsFloat();
				if (this.MinimumDbh < 0.0F)
                {
					throw new XmlException("Minimum DBH is negative.");
                }
			}
			else if (String.Equals(reader.Name, "backgroundInfestationProbability", StringComparison.Ordinal))
			{
				this.BackgroundInfestationProbability = reader.ReadElementContentAsFloat();
				if (this.MinimumDbh < 0.0F)
				{
					throw new XmlException("Minimum DBH is negative.");
				}
			}
			else if (String.Equals(reader.Name, "stormInfestationProbability", StringComparison.Ordinal))
			{
				this.StormInfestationProbability = reader.ReadElementContentAsFloat();
				if ((this.StormInfestationProbability < 0.0F) || (this.StormInfestationProbability > 1.0F))
				{
					throw new XmlException("Storm infestationprobability is negative or greater than 1.0.");
				}
			}
			else if (String.Equals(reader.Name, "baseWinterMortality", StringComparison.Ordinal))
			{
				this.BaseWinterMortality = reader.ReadElementContentAsFloat();
				if ((this.BaseWinterMortality < 0.0F) || (this.BaseWinterMortality > 1.0F))
				{
					throw new XmlException("Base probability of winter mortality is negative or greater than 1.0.");
				}
			}
			else if (String.Equals(reader.Name, "winterMortalityFormula", StringComparison.Ordinal))
			{
				this.WinterMortalityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "spreadKernelFormula", StringComparison.Ordinal))
			{
				this.SpreadKernelFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "spreadKernelMaxDistance", StringComparison.Ordinal))
			{
				this.SpreadKernelMaxDistance = reader.ReadElementContentAsFloat();
				if (this.SpreadKernelMaxDistance < 0.0F)
                {
					throw new XmlException("Maximum distance beetles can fly (spreadKernelMaxDistance) is negative.");
                }
			}
			else if (String.Equals(reader.Name, "cohortsPerGeneration", StringComparison.Ordinal))
			{
				this.CohortsPerGeneration = reader.ReadElementContentAsInt();
				if (this.CohortsPerGeneration < 0)
				{
					throw new XmlException("Cohorts per generation is negative.");
				}
			}
			else if (String.Equals(reader.Name, "cohortsPerSisterbrood", StringComparison.Ordinal))
			{
				this.CohortsPerSisterbrood = reader.ReadElementContentAsInt();
				if (this.CohortsPerSisterbrood < 0)
				{
					throw new XmlException("Cohorts per sister brood is negative.");
				}
			}
			else if (String.Equals(reader.Name, "colonizeProbabilityFormula", StringComparison.Ordinal))
			{
				this.ColonizeProbabilityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "deadTreeSelectivity", StringComparison.Ordinal))
			{
				this.DeadTreeSelectivity = reader.ReadElementContentAsFloat();
				if ((this.DeadTreeSelectivity < 0.0F) || (this.DeadTreeSelectivity > 1.0F))
				{
					throw new XmlException("Dead tree selectivity is negative or greater than 1.0.");
				}
			}
			else if (String.Equals(reader.Name, "outbreakClimateSensitivityFormula", StringComparison.Ordinal))
			{
				this.OutbreakClimateSensitivityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "outbreakDurationMin", StringComparison.Ordinal))
			{
				this.OutbreakDurationMin = reader.ReadElementContentAsInt();
				if (this.OutbreakDurationMin < 0)
				{
					throw new XmlException("Minimum outbreak duration is negative.");
				}
			}
			else if (String.Equals(reader.Name, "outbreakDurationMax", StringComparison.Ordinal))
			{
				this.OutbreakDurationMax = reader.ReadElementContentAsInt();
				if (this.OutbreakDurationMax < 0)
				{
					throw new XmlException("Maximum outbreak duration is negative.");
				}
			}
			else if (String.Equals(reader.Name, "outbreakDurationMortalityFormula", StringComparison.Ordinal))
			{
				this.OutbreakDurationMortalityFormula = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "initialInfestationProbability", StringComparison.Ordinal))
			{
				this.InitialInfestationProbability = reader.ReadElementContentAsFloat();
				if ((this.InitialInfestationProbability < 0.0F) || (this.InitialInfestationProbability > 1.0F))
				{
					throw new XmlException("Initial infestation probability is negative or greater than 1.0.");
				}
			}
			else if (String.Equals(reader.Name, "referenceClimate", StringComparison.Ordinal))
			{
				this.ReferenceClimate.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "onAfterBarkbeetle", StringComparison.Ordinal))
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
