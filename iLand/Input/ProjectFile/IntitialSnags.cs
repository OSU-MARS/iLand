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
                throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
            }

            switch (reader.Name)
            {
                case "snags":
                    reader.Read();
                    break;
                case "stemC":
                    this.StemCarbon = reader.ReadElementContentAsFloat();
                    if (this.StemCarbon < 0.0F)
                    {
                        throw new XmlException("Standing woody debris carbon is negative.");
                    }
                    break;
                case "stemCN":
                    this.StemCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                    if (this.StemCarbonNitrogenRatio < 0.0F)
                    {
                        throw new XmlException("Standing woody debris carbon:nitrogen ratio is negative.");
                    }
                    break;
                case "snagsPerRU":
                    this.SnagsPerResourceUnit = reader.ReadElementContentAsFloat();
                    if (this.SnagsPerResourceUnit < 0.0F)
                    {
                        throw new XmlException("Negative numner of snags per resource unit.");
                    }
                    break;
                case "branchRootC":
                    this.BranchRootCarbon = reader.ReadElementContentAsFloat();
                    if (this.BranchRootCarbon < 0.0F)
                    {
                        throw new XmlException("Branch and root carbon is negative.");
                    }
                    break;
                case "branchRootCN":
                    this.BranchRootCarbonNitrogenRatio = reader.ReadElementContentAsFloat();
                    if (this.BranchRootCarbonNitrogenRatio < 0.0F)
                    {
                        throw new XmlException("Branch and root biomass carbon:nitrogen ratio is negative.");
                    }
                    break;
                case "stemDecompRate":
                    this.StemDecompositionRate = reader.ReadElementContentAsFloat();
                    if (this.StemDecompositionRate < 0.0F)
                    {
                        throw new XmlException("Standing woody debris decomposition rate is negative.");
                    }
                    break;
                case "branchRootDecompRate":
                    this.BranchRootDecompositionRate = reader.ReadElementContentAsFloat();
                    if (this.BranchRootDecompositionRate < 0.0F)
                    {
                        throw new XmlException("Wood decomposition rate is negative.");
                    }
                    break;
                case "snagHalfLife":
                    this.SnagHalfLife = reader.ReadElementContentAsFloat();
                    if (this.SnagHalfLife < 0.0F)
                    {
                        throw new XmlException("Half life of standing woody debris is negative.");
                    }
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
        }
    }
}
