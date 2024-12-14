using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class ModelSettings : XmlSerializable
    {
		// if true, snag dynamics and soil CN cycle is modelled
		public bool CarbonCycleEnabled { get; private set; }

        /// <summary>
        /// GIS projection string for project inputs and outputs.
        /// </summary>
		/// <remarks>
		/// Since iLand inputs do not use spatial data formats, no input file indicates the GIS coordinate system used for 
		/// resource units or trees. iLand therefore does not know what coordinate system to indicate when writing spatial
		/// outputs such as GeoTIFFs. As a workaround, a projection string (e.g. "EPSG:nnnn") must be specified here when 
		/// spatial outputs are enabled.
		/// </remarks>
        public string? CoordinateSystem { get; private set; }

        // linearization of expressions: if true *and* linearize() is explicitely called, then
        // function results will be cached over a defined range of values.
        public bool ExpressionLinearizationEnabled { get; private set; }

		// if false, trees will apply/read light patterns, but do not grow
		public bool GrowthEnabled { get; private set; }

		public int MaxComputeThreads { get; private set; }

		// if false, no natural (intrinsic+stress) mortality occurs
		public bool MortalityEnabled { get; private set; }

		public float OverrideGppPerYear { get; private set; }
        
		public int? RandomSeed { get; private set; }

		// if true, seed dispersal, establishment, ... is modelled
		public bool RegenerationEnabled { get; private set; }

		public string? ScheduledEventsFileName { get; private set; }

		public int SimdWidth { get; private set; }

        public float SoilPermanentWiltPotentialInKPA { get; private set; } // matric potential for residual soil water, kPa
		public float SoilSaturationPotentialInKPa { get; private set; } // matric potential, kPa

		public string SvdStructure { get; private set; } // TODO: make enum
		public int SvdFunction { get; private set; } // TODO: make enum

		// if true, the 'correct' version of the calculation of belowground allocation is used
		public bool UseParFractionBelowGroundAllocation { get; private set; }

		public ModelSettings()
        {
			this.CarbonCycleEnabled = true;
			this.ExpressionLinearizationEnabled = false;
			this.GrowthEnabled = true;
			this.MortalityEnabled = true;
			this.MaxComputeThreads = Environment.ProcessorCount / 2; // one thread per core, assuming a hyperthreaded processor with only p-cores
			this.OverrideGppPerYear = Constant.NoDataFloat;
            this.CoordinateSystem = null;
            this.RandomSeed = null;
			this.RegenerationEnabled = false;
			this.ScheduledEventsFileName = null;
			this.SimdWidth = 256;
			this.SoilPermanentWiltPotentialInKPA = -4000.0F;
			this.SoilSaturationPotentialInKPa = Single.NaN; // C++ uses hard coded default of -15.0F kPa plus a switch
			this.SvdFunction = 3;
			this.SvdStructure = "4m";
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
                case "carbonCycleEnabled":
                    this.CarbonCycleEnabled = reader.ReadElementContentAsBoolean();
                    break;
                case "coordinateSystem":
                    this.CoordinateSystem = reader.ReadElementContentAsString().Trim();
                    if (String.IsNullOrWhiteSpace(this.CoordinateSystem))
                    {
                        throw new XmlException("projection element is present but is empty.");
                    }
                    break;
                case "expressionLinearizationEnabled":
                    this.ExpressionLinearizationEnabled = reader.ReadElementContentAsBoolean();
                    break;
                case "growthEnabled":
					this.GrowthEnabled = reader.ReadElementContentAsBoolean();
					break;
                case "maxComputeThreads":
                    this.MaxComputeThreads = reader.ReadElementContentAsInt();
                    break;
                case "mortalityEnabled":
                    this.MortalityEnabled = reader.ReadElementContentAsBoolean();
                    break;
                case "overrideGppPerYear":
                    this.OverrideGppPerYear = reader.ReadElementContentAsFloat();
                    if (this.OverrideGppPerYear < 0.0F)
                    {
                        throw new XmlException("Fixed annual GPP override is negative.");
                    }
                    break;
                case "randomSeed":
                    this.RandomSeed = reader.ReadElementContentAsInt();
                    // no restriction on range of values
                    break;
                case "scheduledEventsFileName":
                    this.ScheduledEventsFileName = reader.ReadElementContentAsString().Trim();
                    break;
                case "regenerationEnabled":
					this.RegenerationEnabled = reader.ReadElementContentAsBoolean();
					break;
				case "simdWidth":
					this.SimdWidth = reader.ReadElementContentAsInt();
					if ((this.SimdWidth != 32) && (this.SimdWidth != 128) && (this.SimdWidth != 256))
					{
						throw new XmlException("SIMD width must be either 32, 128, or 256 bits.");
					}
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
                case "svdStructure":
                    this.SvdStructure = reader.ReadElementContentAsString();
                    if ((String.Equals(this.SvdStructure, "2m") == false) && (String.Equals(this.SvdStructure, "4m") == false))
                    {
                        throw new XmlException("svdStructure is '" + this.SvdStructure + "'. Valid values are '2m' and '4m'.");
                    }
                    break;
                case "svdFunction":
                    this.SvdFunction = reader.ReadElementContentAsInt();
					if ((this.SvdFunction != 3) && (this.SvdFunction != 5))
					{
                        throw new XmlException("svdFunction is " + this.SvdFunction + ". Valid values are 3 or 5.");
                    }
                    break;
                default:
					throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
