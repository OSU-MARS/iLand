using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class IntitialSnags : XmlSerializable
    {
        public float BranchRootCarbon { get; private set; }
        public float BranchRootCarbonNitrogenRatio { get; private set; }
        public float BranchRootDecompositionRate { get; private set; }
        public float SnagHalfLife { get; private set; }
        public float SnagsPerResourceUnit { get; private set; }
        public float StemCarbon { get; private set; }
		public float StemCarbonNitrogenRatio { get; private set; }
		public float StemDecompositionRate { get; private set; }

        public IntitialSnags()
        {
			this.BranchRootCarbon = 0.0F;
			this.BranchRootCarbonNitrogenRatio = 50.0F;
            this.BranchRootDecompositionRate = 0.0F;
            this.SnagHalfLife = 0.0F;
            this.SnagsPerResourceUnit = 0;
			this.StemCarbon = 0.0F;
			this.StemCarbonNitrogenRatio = 50.0F;
			this.StemDecompositionRate = 0.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (String.Equals(reader.Name, "snags", StringComparison.Ordinal))
            {
                reader.Read();
            }
            else if (String.Equals(reader.Name, "stemC", StringComparison.Ordinal))
            {
                this.StemCarbon = reader.ReadElementContentAsFloat();
                if (this.StemCarbon < 0.0F)
                {
                    throw new XmlException("Standing woody debris carbon is negative.");
                }
            }
            else if (String.Equals(reader.Name, "stemCN", StringComparison.Ordinal))
            {
                this.StemCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                if (this.StemCarbonNitrogenRatio < 0.0F)
                {
                    throw new XmlException("Standing woody debris carbon:nitrogen ratio is negative.");
                }
            }
            else if (String.Equals(reader.Name, "snagsPerRU", StringComparison.Ordinal))
            {
                this.SnagsPerResourceUnit = reader.ReadElementContentAsFloat();
                if (this.SnagsPerResourceUnit < 0.0F)
                {
                    throw new XmlException("Negative numner of snags per resource unit.");
                }
            }
            else if (String.Equals(reader.Name, "branchRootC", StringComparison.Ordinal))
            {
                this.BranchRootCarbon = reader.ReadElementContentAsFloat();
                if (this.BranchRootCarbon < 0.0F)
                {
                    throw new XmlException("Branch and root carbon is negative.");
                }
            }
            else if (String.Equals(reader.Name, "branchRootCN", StringComparison.Ordinal))
            {
                this.BranchRootCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                if (this.BranchRootCarbonNitrogenRatio < 0.0F)
                {
                    throw new XmlException("Branch and root biomass carbon:nitrogen ratio is negative.");
                }
            }
            else if (String.Equals(reader.Name, "stemDecompRate", StringComparison.Ordinal))
            {
                this.StemDecompositionRate = reader.ReadElementContentAsFloat();
                if (this.StemDecompositionRate < 0.0F)
                {
                    throw new XmlException("Standing woody debris decomposition rate is negative.");
                }
            }
            else if (String.Equals(reader.Name, "branchRootDecompRate", StringComparison.Ordinal))
            {
                this.BranchRootDecompositionRate = reader.ReadElementContentAsFloat();
                if (this.BranchRootDecompositionRate < 0.0F)
                {
                    throw new XmlException("Wood decomposition rate is negative.");
                }
            }
            else if (String.Equals(reader.Name, "snagHalfLife", StringComparison.Ordinal))
            {
                this.SnagHalfLife = reader.ReadElementContentAsFloat();
                if (this.SnagHalfLife < 0.0F)
                {
                    throw new XmlException("Half life of standing woody debris is negative.");
                }
            }
            else
			{
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
