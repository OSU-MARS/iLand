using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class SeedDispersal : XmlSerializable
    {
		public SeedBelt SeedBelt { get; private set; }
        public bool DumpSeedMapsEnabled { get; private set; }
		public string? DumpSeedMapsPath { get; private set; }
		public string? ExternalSeedBackgroundInput { get; private set; }
		public bool ExternalSeedEnabled { get; private set; }
		public string? ExternalSeedSource { get; private set; }
		public string? ExternalSeedSpecies { get; private set; }
		public string? ExternalSeedBuffer { get; private set; }
		public double RecruitmentDimensionVariation { get; private set; }
		public LongDistanceDispersal LongDistanceDispersal { get; private set; }
		
		public SeedDispersal()
        {
			this.DumpSeedMapsEnabled = false;
			this.DumpSeedMapsPath = null;
			this.ExternalSeedBackgroundInput = null;
			this.ExternalSeedBuffer = null;
			this.ExternalSeedEnabled = false;
			this.ExternalSeedSource = null;
			this.ExternalSeedSpecies = null;
			this.LongDistanceDispersal = new LongDistanceDispersal();
			this.RecruitmentDimensionVariation = 0.1; // +/- 10%
			this.SeedBelt = new SeedBelt();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("seedDispersal"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("seedBelt"))
			{
				this.SeedBelt.ReadXml(reader);
			}
			else if (reader.IsStartElement("dumpSeedMapsEnabled"))
			{
				this.DumpSeedMapsEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("dumpSeedMapsPath"))
			{
				this.DumpSeedMapsPath = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("externalSeedBackgroundInput"))
			{
				this.ExternalSeedBackgroundInput = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("externalSeedEnabled"))
			{
				this.ExternalSeedEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (reader.IsStartElement("externalSeedSource"))
			{
				this.ExternalSeedSource = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("externalSeedSpecies"))
			{
				this.ExternalSeedSpecies = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("externalSeedBuffer"))
			{
				this.ExternalSeedBuffer = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("recruitmentDimensionVariation"))
			{
				this.RecruitmentDimensionVariation = reader.ReadElementContentAsDouble();
				if (this.RecruitmentDimensionVariation < 0.0)
                {
					throw new XmlException("Variation in sapling recruitment dimensions is negative.");
                }
			}
			else if (reader.IsStartElement("longDistanceDispersal"))
			{
				this.LongDistanceDispersal.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
