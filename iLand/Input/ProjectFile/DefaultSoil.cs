using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class DefaultSoil : XmlSerializable
    {
        public float AvailableNitrogen { get; private set; }
        public float Leaching { get; private set; }
        public float NitrogenDeposition { get; private set; }
        public bool UseDynamicAvailableNitrogen { get; private set; }

        public float El { get; private set; }
        public float Er { get; private set; }
        public float Qb { get; private set; }
        public float Qh { get; private set; }

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
            this.Leaching = 0.15F;
            this.NitrogenDeposition = 0.0F;
            this.UseDynamicAvailableNitrogen = false;

            this.El = 0.0577F;
            this.Er = 0.073F;
            this.Qb = 5.0F;
            this.Qh = 25.0F;

            this.SoilHumificationRate = 0.3F;
            this.SoilOrganicMatterDecompositionRate = 0.02F;
            this.YoungRefractoryDecompositionRate = -1.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
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
            else if (String.Equals(reader.Name, "pctSand", StringComparison.Ordinal))
            {
                this.PercentSand = reader.ReadElementContentAsFloat();
                if ((this.PercentSand < 0.0F) || (this.PercentSand > 100.0F))
                {
                    throw new XmlException("Soil sand content is negative or greater than 100%.");
                }
            }
            else if (String.Equals(reader.Name, "pctSilt", StringComparison.Ordinal))
            {
                this.PercentSilt = reader.ReadElementContentAsFloat();
                if ((this.PercentSilt < 0.0F) || (this.PercentSilt > 100.0F))
                {
                    throw new XmlException("Soil silt content is negative or greater than 100%.");
                }
            }
            else if (String.Equals(reader.Name, "pctClay", StringComparison.Ordinal))
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
            else if (String.Equals(reader.Name, "somC", StringComparison.Ordinal))
            {
                this.SoilOrganicMatterCarbon = reader.ReadElementContentAsFloat();
                if (this.SoilOrganicMatterCarbon < 0.0F)
                {
                    throw new XmlException("Soil organic matter carbon is negative.");
                }
            }
            else if (String.Equals(reader.Name, "somN", StringComparison.Ordinal))
            {
                this.SoilOrganicMatterNitrogen = reader.ReadElementContentAsFloat();
                if (this.SoilOrganicMatterNitrogen < 0.0F)
                {
                    throw new XmlException("Soil organic matter nitrogen is negative.");
                }
            }
            else if (String.Equals(reader.Name, "somDecompRate", StringComparison.Ordinal))
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
                this.El = reader.ReadElementContentAsFloat();
                if (this.El < 0.0F)
                {
                    throw new XmlException("El is negative.");
                }
            }
            else if (String.Equals(reader.Name, "er", StringComparison.Ordinal))
            {
                this.Er = reader.ReadElementContentAsFloat();
                if (this.Er < 0.0F)
                {
                    throw new XmlException("Er is negative.");
                }
            }
            else if (String.Equals(reader.Name, "qb", StringComparison.Ordinal))
            {
                this.Qb = reader.ReadElementContentAsFloat();
                if (this.Qb < 0.0F)
                {
                    throw new XmlException("Qb is negative.");
                }
            }
            else if (String.Equals(reader.Name, "qh", StringComparison.Ordinal))
            {
                this.Qh = reader.ReadElementContentAsFloat();
                if (this.Qh < 0.0F)
                {
                    throw new XmlException("Qh is negative.");
                }
            }
            else if (String.Equals(reader.Name, "swdDBHClass12", StringComparison.Ordinal))
            {
                this.SnagDbhBreakpointSmallMedium = reader.ReadElementContentAsFloat();
                if (this.SnagDbhBreakpointSmallMedium < 0.0F)
                {
                    throw new XmlException("Breakpoint between DBH classes 1 and 2 is negative.");
                }
            }
            else if (String.Equals(reader.Name, "swdDBHClass23", StringComparison.Ordinal))
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
            else if (String.Equals(reader.Name, "leaching", StringComparison.Ordinal))
            {
                this.Leaching = reader.ReadElementContentAsFloat();
                if (this.Leaching < 0.0F)
                {
                    throw new XmlException("Leaching rate is negative.");
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
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
