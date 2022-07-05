using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class DefaultSoil : XmlSerializable
    {
        public float AvailableNitrogen { get; private set; }
        public float NitrogenLeachingFraction { get; private set; }
        public float AnnualNitrogenDeposition { get; private set; }
        public bool UseDynamicAvailableNitrogen { get; private set; }

        public float Depth { get; private set; }

        public float ThetaR { get; private set; }
        public float ThetaS { get; private set; }
        public float VanGenuchtenAlpha { get; private set; }
        public float VanGenuchtenN { get; private set; }

        public float PercentSand { get; private set; }
        public float PercentSilt { get; private set; }
        public float PercentClay { get; private set; }

        public float HumificationRate { get; private set; }
        public float MicrobialLabileEfficiency { get; private set; }
        public float MicrobialRefractoryEfficiency { get; private set; }
        public float MicrobeCarbonNitrogenRatio { get; private set; }
        public float OrganicMatterCarbonNitrogenRatio { get; private set; }
        public float OrganicMatterCarbon { get; private set; }
        public float OrganicMatterNitrogen { get; private set; }
        public float OrganicMatterDecompositionRate { get; private set; }
        public float YoungLabileCarbon { get; private set; }
        public float YoungLabileNitrogen { get; private set; }
        public float YoungLabileDecompositionRate { get; private set; }
        public float YoungRefractoryCarbon { get; private set; }
        public float YoungRefractoryNitrogen { get; private set; }
        public float YoungRefractoryDecompositionRate { get; private set; }

        public DefaultSoil()
        {
            this.AvailableNitrogen = Single.NaN;
            this.AnnualNitrogenDeposition = 0.0F;
            this.NitrogenLeachingFraction = 0.0015F;
            this.UseDynamicAvailableNitrogen = false;

            this.Depth = Single.NaN;

            this.PercentClay = Single.NaN;
            this.PercentSand = Single.NaN;
            this.PercentSilt = Single.NaN;

            this.ThetaR = Single.NaN;
            this.ThetaS = Single.NaN;
            this.VanGenuchtenAlpha = Single.NaN;
            this.VanGenuchtenN = Single.NaN;

            this.HumificationRate = 0.3F;
            this.MicrobeCarbonNitrogenRatio = 5.0F;
            this.MicrobialLabileEfficiency = 0.0577F;
            this.MicrobialRefractoryEfficiency = 0.073F;
            this.OrganicMatterCarbon = Single.NaN;
            this.OrganicMatterCarbonNitrogenRatio = 25.0F;
            this.OrganicMatterDecompositionRate = 0.02F;
            this.OrganicMatterNitrogen = Single.NaN;

            this.YoungLabileCarbon = Single.NaN;
            this.YoungLabileDecompositionRate = Single.NaN;
            this.YoungLabileNitrogen = Single.NaN;
            this.YoungRefractoryCarbon = Single.NaN;
            this.YoungRefractoryDecompositionRate = -1.0F;
            this.YoungRefractoryNitrogen = Single.NaN;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "defaultSoil":
                    reader.Read();
                    break;
                case "annualNitrogenDeposition":
                    this.AnnualNitrogenDeposition = reader.ReadElementContentAsFloat();
                    if (this.AnnualNitrogenDeposition < 0.0F)
                    {
                        throw new XmlException("Nitrogen deposition rate is negative.");
                    }
                    break;
                case "availableNitrogen":
                    this.AvailableNitrogen = reader.ReadElementContentAsFloat();
                    if (this.AvailableNitrogen < 0.0F)
                    {
                        throw new XmlException("Available nitrogen is negative.");
                    }
                    break;
                case "depth":
                    this.Depth = reader.ReadElementContentAsFloat();
                    if (this.Depth <= 0.0F)
                    {
                        throw new XmlException("Soil depth is zero or negative.");
                    }
                    break;
                case "el":
                    this.MicrobialLabileEfficiency = reader.ReadElementContentAsFloat();
                    if (this.MicrobialLabileEfficiency < 0.0F)
                    {
                        throw new XmlException("El is negative.");
                    }
                    break;
                case "er":
                    this.MicrobialRefractoryEfficiency = reader.ReadElementContentAsFloat();
                    if (this.MicrobialRefractoryEfficiency < 0.0F)
                    {
                        throw new XmlException("Er is negative.");
                    }
                    break;
                case "humificationRate":
                    this.HumificationRate = reader.ReadElementContentAsFloat();
                    if (this.HumificationRate < 0.0F)
                    {
                        throw new XmlException("Soil humification rate is negative.");
                    }
                    break;
                case "nitrogenLeachingFraction":
                    this.NitrogenLeachingFraction = reader.ReadElementContentAsFloat();
                    if ((this.NitrogenLeachingFraction < 0.0F || this.NitrogenLeachingFraction > 1.0))
                    {
                        throw new XmlException("Fraction of nitrogen in soil humus lost to annual leaching is negative or greater than 1.0.");
                    }
                    break;
                case "organicMatterC":
                    this.OrganicMatterCarbon = reader.ReadElementContentAsFloat();
                    if (this.OrganicMatterCarbon < 0.0F)
                    {
                        throw new XmlException("Soil organic matter carbon is negative.");
                    }
                    break;
                case "organicMatterN":
                    this.OrganicMatterNitrogen = reader.ReadElementContentAsFloat();
                    if (this.OrganicMatterNitrogen < 0.0F)
                    {
                        throw new XmlException("Soil organic matter nitrogen is negative.");
                    }
                    break;
                case "organicMatterDecompRate":
                    this.OrganicMatterDecompositionRate = reader.ReadElementContentAsFloat();
                    if (this.OrganicMatterDecompositionRate < 0.0F)
                    {
                        throw new XmlException("Soil organic matter decomposition rate is negative.");
                    }
                    break;
                case "percentSand":
                    this.PercentSand = reader.ReadElementContentAsFloat();
                    if ((this.PercentSand < 0.0F) || (this.PercentSand > 100.0F))
                    {
                        throw new XmlException("Soil sand content is negative or greater than 100%.");
                    }
                    break;
                case "percentSilt":
                    this.PercentSilt = reader.ReadElementContentAsFloat();
                    if ((this.PercentSilt < 0.0F) || (this.PercentSilt > 100.0F))
                    {
                        throw new XmlException("Soil silt content is negative or greater than 100%.");
                    }
                    break;
                case "percentClay":
                    this.PercentClay = reader.ReadElementContentAsFloat();
                    if ((this.PercentClay < 0.0F) || (this.PercentClay > 100.0F))
                    {
                        throw new XmlException("Soil clay content is negative or greater than 100%.");
                    }
                    break;
                case "qb":
                    this.MicrobeCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                    if (this.MicrobeCarbonNitrogenRatio < 0.0F)
                    {
                        throw new XmlException("Qb is negative.");
                    }
                    break;
                case "qh":
                    this.OrganicMatterCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                    if (this.OrganicMatterCarbonNitrogenRatio < 0.0F)
                    {
                        throw new XmlException("Qh is negative.");
                    }
                    break;
                case "youngLabileC":
                    this.YoungLabileCarbon = reader.ReadElementContentAsFloat();
                    if (this.YoungLabileCarbon < 0.0F)
                    {
                        throw new XmlException("Young labile carbon is negative.");
                    }
                    break;
                case "youngLabileN":
                    this.YoungLabileNitrogen = reader.ReadElementContentAsFloat();
                    if (this.YoungLabileNitrogen < 0.0F)
                    {
                        throw new XmlException("Young labile nitrogen is negative.");
                    }
                    break;
                case "youngLabileDecompRate":
                    this.YoungLabileDecompositionRate = reader.ReadElementContentAsFloat();
                    if (this.YoungLabileDecompositionRate < 0.0F)
                    {
                        throw new XmlException("Young labile decomposition rate is negative.");
                    }
                    break;
                case "youngRefractoryC":
                    this.YoungRefractoryCarbon = reader.ReadElementContentAsFloat();
                    if (this.YoungRefractoryCarbon < 0.0F)
                    {
                        throw new XmlException("Young refractory carbon is negative.");
                    }
                    break;
                case "youngRefractoryN":
                    this.YoungRefractoryNitrogen = reader.ReadElementContentAsFloat();
                    if (this.YoungRefractoryNitrogen < 0.0F)
                    {
                        throw new XmlException("Young refractory nitrogen is negative.");
                    }
                    break;
                case "youngRefractoryDecompRate":
                    this.YoungRefractoryDecompositionRate = reader.ReadElementContentAsFloat();
                    if (this.YoungRefractoryDecompositionRate < 0.0F)
                    {
                        throw new XmlException("Young refractory decomposition rate is negative.");
                    }
                    break;
                case "useDynamicAvailableNitrogen":
                    this.UseDynamicAvailableNitrogen = reader.ReadElementContentAsBoolean();
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
