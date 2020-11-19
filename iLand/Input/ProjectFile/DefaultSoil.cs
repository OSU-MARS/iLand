using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class DefaultSoil : XmlSerializable
    {
        public float AvailableNitrogen { get; private set; }
        public float NitrogenLeachingFraction { get; private set; }
        public float NitrogenDeposition { get; private set; }
        public bool UseDynamicAvailableNitrogen { get; private set; }

        public float MicrobialLabileEfficiency { get; private set; }
        public float MicrobialRefractoryEfficiency { get; private set; }
        public float SoilMicrobeCarbonNitrogenRatio { get; private set; }
        public float SoilOrganicMatterCarbonNitrogenRatio { get; private set; }

        public float PercentSand { get; private set; }
        public float PercentSilt { get; private set; }
        public float PercentClay { get; private set; }
        public float SoilDepth { get; private set; }

        public float YoungLabileCarbon { get; private set; }
        public float YoungLabileNitrogen { get; private set; }
        public float YoungLabileDecompositionRate { get; private set; }
        public float YoungRefractoryCarbon { get; private set; }
        public float YoungRefractoryNitrogen { get; private set; }
        public float YoungRefractoryDecompositionRate { get; private set; }
        public float SoilOrganicMatterCarbon { get; private set; }
        public float SoilOrganicMatterNitrogen { get; private set; }
        public float SoilOrganicMatterDecompositionRate { get; private set; }
        public float SoilHumificationRate { get; private set; }

        public float SnagDbhBreakpointSmallMedium { get; private set; }
        public float SnagDdhBreakpointMediumLarge { get; private set; }

        public DefaultSoil()
        {
            this.NitrogenLeachingFraction = 0.0015F;
            this.NitrogenDeposition = 0.0F;
            this.UseDynamicAvailableNitrogen = false;

            this.MicrobialLabileEfficiency = 0.0577F;
            this.MicrobialRefractoryEfficiency = 0.073F;
            this.SoilMicrobeCarbonNitrogenRatio = 5.0F;
            this.SoilOrganicMatterCarbonNitrogenRatio = 25.0F;

            this.SnagDbhBreakpointSmallMedium = 20.0F;
            this.SnagDdhBreakpointMediumLarge = 100.0F;
            this.SoilHumificationRate = 0.3F;
            this.SoilOrganicMatterDecompositionRate = 0.02F;
            this.YoungRefractoryDecompositionRate = -1.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            if (String.Equals(reader.Name, "defaultSoil", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "availableNitrogen", StringComparison.Ordinal))
            {
                this.AvailableNitrogen = reader.ReadElementContentAsFloat();
                if (this.AvailableNitrogen < 0.0F)
                {
                    throw new XmlException("Available nitrogen is negative.");
                }
            }
            else if (String.Equals(reader.Name, "soilDepth", StringComparison.Ordinal))
            {
                this.SoilDepth = reader.ReadElementContentAsFloat();
                if (this.SoilDepth <= 0.0F)
                {
                    throw new XmlException("Soil depth is zero or negative.");
                }
            }
            else if (String.Equals(reader.Name, "percentSand", StringComparison.Ordinal))
            {
                this.PercentSand = reader.ReadElementContentAsFloat();
                if ((this.PercentSand < 0.0F) || (this.PercentSand > 100.0F))
                {
                    throw new XmlException("Soil sand content is negative or greater than 100%.");
                }
            }
            else if (String.Equals(reader.Name, "percentSilt", StringComparison.Ordinal))
            {
                this.PercentSilt = reader.ReadElementContentAsFloat();
                if ((this.PercentSilt < 0.0F) || (this.PercentSilt > 100.0F))
                {
                    throw new XmlException("Soil silt content is negative or greater than 100%.");
                }
            }
            else if (String.Equals(reader.Name, "percentClay", StringComparison.Ordinal))
            {
                this.PercentClay = reader.ReadElementContentAsFloat();
                if ((this.PercentClay < 0.0F) || (this.PercentClay > 100.0F))
                {
                    throw new XmlException("Soil clay content is negative or greater than 100%.");
                }
            }
            else if (String.Equals(reader.Name, "youngLabileC", StringComparison.Ordinal))
            {
                this.YoungLabileCarbon = reader.ReadElementContentAsFloat();
                if (this.YoungLabileCarbon < 0.0F)
                {
                    throw new XmlException("Young labile carbon is negative.");
                }
            }
            else if (String.Equals(reader.Name, "youngLabileN", StringComparison.Ordinal))
            {
                this.YoungLabileNitrogen = reader.ReadElementContentAsFloat();
                if (this.YoungLabileNitrogen < 0.0F)
                {
                    throw new XmlException("Young labile nitrogen is negative.");
                }
            }
            else if (String.Equals(reader.Name, "youngLabileDecompRate", StringComparison.Ordinal))
            {
                this.YoungLabileDecompositionRate = reader.ReadElementContentAsFloat();
                if (this.YoungLabileDecompositionRate < 0.0F)
                {
                    throw new XmlException("Young labile decomposition rate is negative.");
                }
            }
            else if (String.Equals(reader.Name, "youngRefractoryC", StringComparison.Ordinal))
            {
                this.YoungRefractoryCarbon = reader.ReadElementContentAsFloat();
                if (this.YoungRefractoryCarbon < 0.0F)
                {
                    throw new XmlException("Young refractory carbon is negative.");
                }
            }
            else if (String.Equals(reader.Name, "youngRefractoryN", StringComparison.Ordinal))
            {
                this.YoungRefractoryNitrogen = reader.ReadElementContentAsFloat();
                if (this.YoungRefractoryNitrogen < 0.0F)
                {
                    throw new XmlException("Young refractory nitrogen is negative.");
                }
            }
            else if (String.Equals(reader.Name, "youngRefractoryDecompRate", StringComparison.Ordinal))
            {
                this.YoungRefractoryDecompositionRate = reader.ReadElementContentAsFloat();
                if (this.YoungRefractoryDecompositionRate < 0.0F)
                {
                    throw new XmlException("Young refractory decomposition rate is negative.");
                }
            }
            else if (String.Equals(reader.Name, "soilOrganicMatterC", StringComparison.Ordinal))
            {
                this.SoilOrganicMatterCarbon = reader.ReadElementContentAsFloat();
                if (this.SoilOrganicMatterCarbon < 0.0F)
                {
                    throw new XmlException("Soil organic matter carbon is negative.");
                }
            }
            else if (String.Equals(reader.Name, "soilOrganicMatterN", StringComparison.Ordinal))
            {
                this.SoilOrganicMatterNitrogen = reader.ReadElementContentAsFloat();
                if (this.SoilOrganicMatterNitrogen < 0.0F)
                {
                    throw new XmlException("Soil organic matter nitrogen is negative.");
                }
            }
            else if (String.Equals(reader.Name, "soilOrganicMatterDecompRate", StringComparison.Ordinal))
            {
                this.SoilOrganicMatterDecompositionRate = reader.ReadElementContentAsFloat();
                if (this.SoilOrganicMatterDecompositionRate < 0.0F)
                {
                    throw new XmlException("Soil organic matter decomposition rate is negative.");
                }
            }
            else if (String.Equals(reader.Name, "soilHumificationRate", StringComparison.Ordinal))
            {
                this.SoilHumificationRate = reader.ReadElementContentAsFloat();
                if (this.SoilHumificationRate < 0.0F)
                {
                    throw new XmlException("Soil humification rate is negative.");
                }
            }
            else if (String.Equals(reader.Name, "el", StringComparison.Ordinal))
            {
                this.MicrobialLabileEfficiency = reader.ReadElementContentAsFloat();
                if (this.MicrobialLabileEfficiency < 0.0F)
                {
                    throw new XmlException("El is negative.");
                }
            }
            else if (String.Equals(reader.Name, "er", StringComparison.Ordinal))
            {
                this.MicrobialRefractoryEfficiency = reader.ReadElementContentAsFloat();
                if (this.MicrobialRefractoryEfficiency < 0.0F)
                {
                    throw new XmlException("Er is negative.");
                }
            }
            else if (String.Equals(reader.Name, "qb", StringComparison.Ordinal))
            {
                this.SoilMicrobeCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                if (this.SoilMicrobeCarbonNitrogenRatio < 0.0F)
                {
                    throw new XmlException("Qb is negative.");
                }
            }
            else if (String.Equals(reader.Name, "qh", StringComparison.Ordinal))
            {
                this.SoilOrganicMatterCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                if (this.SoilOrganicMatterCarbonNitrogenRatio < 0.0F)
                {
                    throw new XmlException("Qh is negative.");
                }
            }
            else if (String.Equals(reader.Name, "snagSmallMediumDbhBreakpoint", StringComparison.Ordinal))
            {
                this.SnagDbhBreakpointSmallMedium = reader.ReadElementContentAsFloat();
                if (this.SnagDbhBreakpointSmallMedium < 0.0F)
                {
                    throw new XmlException("Breakpoint between DBH classes 1 and 2 is negative.");
                }
            }
            else if (String.Equals(reader.Name, "snagMediumLargeDbhBreakpoint", StringComparison.Ordinal))
            {
                this.SnagDdhBreakpointMediumLarge = reader.ReadElementContentAsFloat();
                if (this.SnagDdhBreakpointMediumLarge < 0.0F)
                {
                    throw new XmlException("Breakpoint between DBH classes 2 and 3 is negative.");
                }
            }
            else if (String.Equals(reader.Name, "useDynamicAvailableNitrogen", StringComparison.Ordinal))
            {
                this.UseDynamicAvailableNitrogen = reader.ReadElementContentAsBoolean();
            }
            else if (String.Equals(reader.Name, "nitrogenLeachingFraction", StringComparison.Ordinal))
            {
                this.NitrogenLeachingFraction = reader.ReadElementContentAsFloat();
                if ((this.NitrogenLeachingFraction < 0.0F || this.NitrogenLeachingFraction > 1.0))
                {
                    throw new XmlException("Fraction of nitrogen in soil humus lost to annual leaching is negative or greater than 1.0.");
                }
            }
            else if (String.Equals(reader.Name, "nitrogenDeposition", StringComparison.Ordinal))
            {
                this.NitrogenDeposition = reader.ReadElementContentAsFloat();
                if (this.NitrogenDeposition < 0.0F)
                {
                    throw new XmlException("Nitrogen deposition rate is negative.");
                }
            }
            else
            {
                throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
