using System.Xml;

namespace iLand.Input.ProjectFile
{
    // TODO: called moss but appears to be more of a peat implementation
    public class Moss : XmlSerializable
    {
        // parameters of the moss submodel
        public const float MossMaxProductivity = 0.3F; ///< maximum moss productivity (0.3kg/m2/yr) (Foster et al 2019)
        public const float MossMinBiomass = 0.0001F; // kg/m2
        public const float SLA = 1.0F; ///< specific leaf area of moss (set to 1 m2/kg) TODO: why is this default so low?

        public float Biomass { get; private set; } ///< initial moss biomass in kg/m2
        public float LightK { get; private set; } ///< light extinction coefficient used for canopy+moss layer
        public float LightCompensationPoint { get; private set; } ///< light compensation point (proportion of light level above canopy)
        public float LightSaturationPoint { get; private set; } ///< light saturation point (proportion of light level above canopy)
        public float RespirationQ { get; private set; } ///< parameter of moss respiration function: q: respiration, b: turnover
        public float RespirationB { get; private set; }
        public float CarbonNitrogenRatio { get; private set; } ///< carbon : nitrogen ratio of moss (used for litter input)
        public float BulkDensity { get; private set; } ///< density (kg/m3) of moss layer
        public float DecompositionRate { get; private set; } ///< decomposition rate of moss (compare KYL of species)
        public float DeciduousInhibitionFactor { get; private set; } ///< factor for inhibiting effect of fresh broadleaved litter

        public Moss()
        {
            this.Biomass = 0.05F; // kg/m2
            this.LightK = 0.7F;
            this.LightCompensationPoint = 0.01F;
            this.LightSaturationPoint = 0.05F;
            this.RespirationQ = 0.12F;
            this.RespirationB = 0.136F;
            this.CarbonNitrogenRatio = 30.0F;
            this.BulkDensity = 50.0F;
            this.DecompositionRate = 0.14F;
            this.DeciduousInhibitionFactor = 0.45F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "moss":
                    reader.Read();
                    break;
                case "biomass":
                    this.Biomass = reader.ReadElementContentAsFloat();
                    break;
                case "light_k":
                    this.LightK = reader.ReadElementContentAsFloat();
                    break;
                case "light_comp":
                    this.LightCompensationPoint = reader.ReadElementContentAsFloat();
                    break;
                case "light_sat":
                    this.LightSaturationPoint = reader.ReadElementContentAsFloat();
                    break;
                case "respiration_q":
                    this.RespirationQ = reader.ReadElementContentAsFloat();
                    break;
                case "respiration_b":
                    this.RespirationB = reader.ReadElementContentAsFloat();
                    break;
                case "CNRatio":
                    this.CarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                    break;
                case "bulk_density":
                    this.BulkDensity = reader.ReadElementContentAsFloat();
                    break;
                case "r_decomp":
                    this.DecompositionRate = reader.ReadElementContentAsFloat();
                    break;
                case "r_deciduous_inhibition":
                    this.DeciduousInhibitionFactor = reader.ReadElementContentAsFloat();
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
