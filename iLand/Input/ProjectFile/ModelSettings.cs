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

		public int MaxThreads { get; private set; }

		// if false, no natural (intrinsic+stress) mortality occurs
		public bool MortalityEnabled { get; private set; }

		public float OverrideGppPerYear { get; private set; }
		public int? RandomSeed { get; private set; }

		// if true, seed dispersal, establishment, ... is modelled
		public bool RegenerationEnabled { get; private set; }

		public string? ScheduledEventsFileName { get; private set; }

		public float SoilPermanentWiltPotentialInKPA { get; private set; } // matric potential for residual soil water, kPa
		public float SoilSaturationPotentialInKPa { get; private set; } // matric potential, kPa

		// if true, the 'correct' version of the calculation of belowground allocation is used
		public bool UseParFractionBelowGroundAllocation { get; private set; }

		public ModelSettings()
        {
			this.CarbonCycleEnabled = true;
			this.ExpressionLinearizationEnabled = false;
			this.GrowthEnabled = true;
			this.MortalityEnabled = true;
			this.MaxThreads = Environment.ProcessorCount / 2; // one thread per core, assuming a hyperthreaded processor with only p-cores
			this.OverrideGppPerYear = Constant.NoDataFloat;
			this.RandomSeed = null;
			this.RegenerationEnabled = false;
			this.ScheduledEventsFileName = null;
			this.SoilPermanentWiltPotentialInKPA = -4000.0F;
			this.SoilSaturationPotentialInKPa = Single.NaN; // C++ uses hard coded default of -15.0F kPa plus a switch
			this.UseParFractionBelowGroundAllocation = true;
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			switch (reader.Name)
			{
				case "settings":
					reader.Read();
					break;
				case "mortalityEnabled":
					this.MortalityEnabled = reader.ReadElementContentAsBoolean();
					break;
				case "growthEnabled":
					this.GrowthEnabled = reader.ReadElementContentAsBoolean();
					break;
				case "carbonCycleEnabled":
					this.CarbonCycleEnabled = reader.ReadElementContentAsBoolean();
					break;
				case "regenerationEnabled":
					this.RegenerationEnabled = reader.ReadElementContentAsBoolean();
					break;
				case "soilPermanentWiltPotential":
					this.SoilPermanentWiltPotentialInKPA = reader.ReadElementContentAsFloat();
					break;
				case "soilSaturationPotential":
					this.SoilSaturationPotentialInKPa = reader.ReadElementContentAsFloat();
					break;
				case "usePARFractionBelowGroundAllocation":
					this.UseParFractionBelowGroundAllocation = reader.ReadElementContentAsBoolean();
					break;
				case "maxThreads":
					this.MaxThreads = reader.ReadElementContentAsInt();
					break;
				case "randomSeed":
					this.RandomSeed = reader.ReadElementContentAsInt();
					// no restriction on range of values
					break;
				case "expressionLinearizationEnabled":
					this.ExpressionLinearizationEnabled = reader.ReadElementContentAsBoolean();
					break;
				case "overrideGppPerYear":
					this.OverrideGppPerYear = reader.ReadElementContentAsFloat();
					if (this.OverrideGppPerYear < 0.0F)
					{
						throw new XmlException("Fixed annual GPP override is negative.");
					}
					break;
				case "scheduledEventsFileName":
					this.ScheduledEventsFileName = reader.ReadElementContentAsString().Trim();
					break;
				default:
					throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
