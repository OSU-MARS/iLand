using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Site : XmlSerializable
	{
		public float AvailableNitrogen { get; private set; }
		public float SoilDepth { get; private set; }
		public float PercentSand { get; private set; }
		public float PercentSilt { get; private set; }
		public float PercentClay { get; private set; }
		public float YoungLabileCarbon { get; private set; }
		public float YoungLabileNitrogen { get; private set; }
		public float YoungLabileDecompRate { get; private set; }
		public float YoungRefractoryCarbon { get; private set; }
		public float YoungRefractoryNitrogen { get; private set; }
		public float YoungRefractoryDecompositionRate { get; private set; }
		public float SoilOrganicMatterCarbon { get; private set; }
		public float SoilOrganicMatterNitrogen { get; private set; }
		public float SoilOrganicMatterDecompositionRate { get; private set; }
		public float SoilHumificationRate { get; private set; }

		public Site()
        {
			this.YoungRefractoryDecompositionRate = -1.0F;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("site"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("availableNitrogen"))
            {
                this.AvailableNitrogen = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("soilDepth"))
            {
                this.SoilDepth = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("pctSand"))
            {
                this.PercentSand = reader.ReadElementContentAsFloat();
            }
            else if (reader.IsStartElement("pctSilt"))
            {
                this.PercentSilt = reader.ReadElementContentAsFloat();
            }
			else if (reader.IsStartElement("pctClay"))
			{
				this.PercentClay = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("youngLabileC"))
			{
				this.YoungLabileCarbon = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("youngLabileN"))
			{
				this.YoungLabileNitrogen = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("youngLabileDecompRate"))
			{
				this.YoungLabileDecompRate = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("youngRefractoryC"))
			{
				this.YoungRefractoryCarbon = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("youngRefractoryN"))
			{
				this.YoungRefractoryNitrogen = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("youngRefractoryDecompRate"))
			{
				this.YoungRefractoryDecompositionRate = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("somC"))
			{
				this.SoilOrganicMatterCarbon = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("somN"))
			{
				this.SoilOrganicMatterNitrogen = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("somDecompRate"))
			{
				this.SoilOrganicMatterDecompositionRate = reader.ReadElementContentAsFloat();
			}
			else if (reader.IsStartElement("soilHumificationRate"))
			{
				this.SoilHumificationRate = reader.ReadElementContentAsFloat();
			}
			else
			{
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
