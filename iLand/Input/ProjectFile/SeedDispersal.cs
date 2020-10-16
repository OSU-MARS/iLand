using System;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class SeedDispersal
    {
		[XmlElement(ElementName = "seedBelt")]
		public SeedBelt SeedBelt { get; set; }

		[XmlElement(ElementName = "dumpSeedMapsEnabled")]
        public bool DumpSeedMapsEnabled { get; set; }

		[XmlElement(ElementName = "dumpSeedMapsPath")]
		public string DumpSeedMapsPath { get; set; }

		[XmlElement(ElementName = "externalSeedBackgroundInput")]
		public string ExternalSeedBackgroundInput { get; set; }

		[XmlElement(ElementName = "externalSeedEnabled")]
		public bool ExternalSeedEnabled { get; set; }

		[XmlElement(ElementName = "externalSeedSource")]
		public string ExternalSeedSource { get; set; }

		[XmlElement(ElementName = "externalSeedSpecies")]
		public string ExternalSeedSpecies { get; set; }

		[XmlElement(ElementName = "externalSeedBuffer")]
		public string ExternalSeedBuffer { get; set; }

		[XmlElement(ElementName = "recruitmentDimensionVariation")]
		public double RecruitmentDimensionVariation { get; set; }

		[XmlElement(ElementName = "longDistanceDispersal")]
		public LongDistanceDispersal LongDistanceDispersal { get; set; }
		
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
    }
}
