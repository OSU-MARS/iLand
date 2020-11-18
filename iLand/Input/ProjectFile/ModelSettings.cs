using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ModelSettings : XmlSerializable
    {
		// if true, snag dynamics and soil CN cycle is modelled
		public bool CarbonCycleEnabled { get; private set; }

		// linearization of expressions: if true *and* linearize() is explicitely called, then
		// function results will be cached over a defined range of values.
		public bool ExpressionLinearizationEnabled { get; private set; }

		// if false, trees will apply/read light patterns, but do not grow
		public bool GrowthEnabled { get; private set; }

		// if false, no natural (intrinsic+stress) mortality occurs
		public bool MortalityEnabled { get; private set; }

		public bool Multithreading { get; private set; }
		public float OverrideGppPerYear { get; private set; }
		public int? RandomSeed { get; private set; }

		// if true, seed dispersal, establishment, ... is modelled
		public bool RegenerationEnabled { get; private set; }

		public string? ScheduledEventsFileName { get; private set; }

		// if true, the 'correct' version of the calculation of belowground allocation is used
		public bool UseParFractionBelowGroundAllocation { get; private set; }

		public bool WaterUseSoilSaturation { get; private set; }

		public ModelSettings()
        {
			this.CarbonCycleEnabled = false;
			this.ExpressionLinearizationEnabled = false;
			this.GrowthEnabled = true;
			this.MortalityEnabled = true;
			this.Multithreading = false;
			this.OverrideGppPerYear = 0.0F;
			this.RandomSeed = null;
			this.RegenerationEnabled = false;
			this.ScheduledEventsFileName = null;
			this.UseParFractionBelowGroundAllocation = true;
			this.WaterUseSoilSaturation = true;
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (String.Equals(reader.Name, "settings", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "regenerationEnabled", StringComparison.Ordinal))
			{
				this.RegenerationEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "mortalityEnabled", StringComparison.Ordinal))
			{
				this.MortalityEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "growthEnabled", StringComparison.Ordinal))
			{
				this.GrowthEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "carbonCycleEnabled", StringComparison.Ordinal))
			{
				this.CarbonCycleEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "regenerationEnabled", StringComparison.Ordinal))
			{
				this.RegenerationEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "waterUseSoilSaturation", StringComparison.Ordinal))
			{
				this.WaterUseSoilSaturation = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "usePARFractionBelowGroundAllocation", StringComparison.Ordinal))
			{
				this.UseParFractionBelowGroundAllocation = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "multithreading", StringComparison.Ordinal))
			{
				this.Multithreading = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "randomSeed", StringComparison.Ordinal))
			{
				this.RandomSeed = reader.ReadElementContentAsInt();
				// no restriction on range of values
			}
			else if (String.Equals(reader.Name, "expressionLinearizationEnabled", StringComparison.Ordinal))
			{
				this.ExpressionLinearizationEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "overrideGppPerYear", StringComparison.Ordinal))
			{
				this.OverrideGppPerYear = reader.ReadElementContentAsFloat();
				if (this.OverrideGppPerYear < 0.0F)
				{
					throw new XmlException("Fixed annual GPP override is negative.");
				}
			}
			else if (String.Equals(reader.Name, "scheduledEventsFileName", StringComparison.Ordinal))
			{
				this.ScheduledEventsFileName = reader.ReadElementContentAsString().Trim();
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
