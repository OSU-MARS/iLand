using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class SeedDispersal : XmlSerializable
    {
		public string? DumpSeedMapsPath { get; private set; }
		public string? ExternalSeedBackgroundInput { get; private set; }
		public bool ExternalSeedEnabled { get; private set; }
		public string? ExternalSeedDirection { get; private set; }
		public string? ExternalSeedSpecies { get; private set; }
		public string? ExternalSeedBuffer { get; private set; }
		public float RecruitmentDimensionVariation { get; private set; }

		public LongDistanceDispersal LongDistanceDispersal { get; private init; }
		public ExternalSeedBelt ExternalSeedBelt { get; private init; }
		
		public SeedDispersal()
        {
			this.DumpSeedMapsPath = null;
			this.ExternalSeedBackgroundInput = null;
			this.ExternalSeedBelt = new ExternalSeedBelt();
			this.ExternalSeedBuffer = null;
			this.ExternalSeedEnabled = false;
			this.ExternalSeedDirection = null;
			this.ExternalSeedSpecies = null;
			this.LongDistanceDispersal = new LongDistanceDispersal();
			this.RecruitmentDimensionVariation = 0.1F; // +/- 10%
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				if (String.Equals(reader.Name, "externalSeedBelt", StringComparison.Ordinal))
				{
					this.ExternalSeedBelt.ReadXml(reader);
				}
				else
				{
					throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else if (String.Equals(reader.Name, "seedDispersal", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "dumpSeedMapsPath", StringComparison.Ordinal))
			{
				this.DumpSeedMapsPath = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "externalSeedBackgroundInput", StringComparison.Ordinal))
			{
				this.ExternalSeedBackgroundInput = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "externalSeedEnabled", StringComparison.Ordinal))
			{
				this.ExternalSeedEnabled = reader.ReadElementContentAsBoolean();
			}
			else if (String.Equals(reader.Name, "externalSeedSource", StringComparison.Ordinal))
			{
				this.ExternalSeedDirection = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "externalSeedSpecies", StringComparison.Ordinal))
			{
				this.ExternalSeedSpecies = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "externalSeedBuffer", StringComparison.Ordinal))
			{
				this.ExternalSeedBuffer = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "recruitmentDimensionVariation", StringComparison.Ordinal))
			{
				this.RecruitmentDimensionVariation = reader.ReadElementContentAsFloat();
				if (this.RecruitmentDimensionVariation < 0.0)
                {
					throw new XmlException("Variation in sapling recruitment dimensions is negative.");
                }
			}
			else if (String.Equals(reader.Name, "longDistanceDispersal", StringComparison.Ordinal))
			{
				this.LongDistanceDispersal.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
