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
				switch (reader.Name)
				{
					case "externalSeedBelt":
						this.ExternalSeedBelt.ReadXml(reader);
						break;
					default:
						throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else
			{
				switch (reader.Name)
				{
					case "seedDispersal":
						reader.Read();
						break;
					case "dumpSeedMapsPath":
						this.DumpSeedMapsPath = reader.ReadElementContentAsString().Trim();
						break;
					case "externalSeedBackgroundInput":
						this.ExternalSeedBackgroundInput = reader.ReadElementContentAsString().Trim();
						break;
					case "externalSeedEnabled":
						this.ExternalSeedEnabled = reader.ReadElementContentAsBoolean();
						break;
					case "externalSeedSource":
						this.ExternalSeedDirection = reader.ReadElementContentAsString().Trim();
						break;
					case "externalSeedSpecies":
						this.ExternalSeedSpecies = reader.ReadElementContentAsString().Trim();
						break;
					case "externalSeedBuffer":
						this.ExternalSeedBuffer = reader.ReadElementContentAsString().Trim();
						break;
					case "recruitmentDimensionVariation":
						this.RecruitmentDimensionVariation = reader.ReadElementContentAsFloat();
						if (this.RecruitmentDimensionVariation < 0.0)
						{
							throw new XmlException("Variation in sapling recruitment dimensions is negative.");
						}
						break;
					case "longDistanceDispersal":
						this.LongDistanceDispersal.ReadXml(reader);
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
		}
	}
}
