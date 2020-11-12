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
		public float YoungLabileDecompositionRate { get; private set; }
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
				if (this.AvailableNitrogen < 0.0F)
				{
					throw new XmlException("Available nitrogen is negative.");
				}
			}
			else if (reader.IsStartElement("soilDepth"))
			{
				this.SoilDepth = reader.ReadElementContentAsFloat();
				if (this.SoilDepth <= 0.0F)
				{
					throw new XmlException("Soil depth is zero or negative.");
				}
			}
			else if (reader.IsStartElement("pctSand"))
			{
				this.PercentSand = reader.ReadElementContentAsFloat();
				if ((this.PercentSand < 0.0F) || (this.PercentSand > 100.0F))
				{
					throw new XmlException("Soil sand content is negative or greater than 100%.");
				}
			}
			else if (reader.IsStartElement("pctSilt"))
			{
				this.PercentSilt = reader.ReadElementContentAsFloat();
				if ((this.PercentSilt < 0.0F) || (this.PercentSilt > 100.0F))
				{
					throw new XmlException("Soil silt content is negative or greater than 100%.");
				}
			}
			else if (reader.IsStartElement("pctClay"))
			{
				this.PercentClay = reader.ReadElementContentAsFloat();
				if ((this.PercentClay < 0.0F) || (this.PercentClay > 100.0F))
				{
					throw new XmlException("Soil clay content is negative or greater than 100%.");
				}
			}
			else if (reader.IsStartElement("youngLabileC"))
			{
				this.YoungLabileCarbon = reader.ReadElementContentAsFloat();
				if (this.YoungLabileCarbon < 0.0F)
				{
					throw new XmlException("Young labile carbon is negative.");
				}
			}
			else if (reader.IsStartElement("youngLabileN"))
			{
				this.YoungLabileNitrogen = reader.ReadElementContentAsFloat();
				if (this.YoungLabileNitrogen < 0.0F)
				{
					throw new XmlException("Young labile nitrogen is negative.");
				}
			}
			else if (reader.IsStartElement("youngLabileDecompRate"))
			{
				this.YoungLabileDecompositionRate = reader.ReadElementContentAsFloat();
				if (this.YoungLabileDecompositionRate < 0.0F)
				{
					throw new XmlException("Young labile decomposition rate is negative.");
				}
			}
			else if (reader.IsStartElement("youngRefractoryC"))
			{
				this.YoungRefractoryCarbon = reader.ReadElementContentAsFloat();
				if (this.YoungRefractoryCarbon < 0.0F)
				{
					throw new XmlException("Young refractory carbon is negative.");
				}
			}
			else if (reader.IsStartElement("youngRefractoryN"))
			{
				this.YoungRefractoryNitrogen = reader.ReadElementContentAsFloat();
				if (this.YoungRefractoryNitrogen < 0.0F)
				{
					throw new XmlException("Young refractory nitrogen is negative.");
				}
			}
			else if (reader.IsStartElement("youngRefractoryDecompRate"))
			{
				this.YoungRefractoryDecompositionRate = reader.ReadElementContentAsFloat();
				if (this.YoungRefractoryDecompositionRate < 0.0F)
				{
					throw new XmlException("Young refractory decomposition rate is negative.");
				}
			}
			else if (reader.IsStartElement("somC"))
			{
				this.SoilOrganicMatterCarbon = reader.ReadElementContentAsFloat();
				if (this.SoilOrganicMatterCarbon < 0.0F)
				{
					throw new XmlException("Soil organic matter carbon is negative.");
				}
			}
			else if (reader.IsStartElement("somN"))
			{
				this.SoilOrganicMatterNitrogen = reader.ReadElementContentAsFloat();
				if (this.SoilOrganicMatterNitrogen < 0.0F)
				{
					throw new XmlException("Soil organic matter nitrogen is negative.");
				}
			}
			else if (reader.IsStartElement("somDecompRate"))
			{
				this.SoilOrganicMatterDecompositionRate = reader.ReadElementContentAsFloat();
				if (this.SoilOrganicMatterDecompositionRate < 0.0F)
				{
					throw new XmlException("Soil organic matter decomposition rate is negative.");
				}
			}
			else if (reader.IsStartElement("soilHumificationRate"))
			{
				this.SoilHumificationRate = reader.ReadElementContentAsFloat();
				if (this.SoilHumificationRate < 0.0F)
				{
					throw new XmlException("Soil humification rate is negative.");
				}
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
        }
    }
}
