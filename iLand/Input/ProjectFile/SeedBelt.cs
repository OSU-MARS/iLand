using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class SeedBelt : Enablable
    {
        [XmlElement(ElementName = "width")]
        public int Width { get; set; }

		[XmlElement(ElementName = "sizeX")]
		public int SizeX { get; set; }

		[XmlElement(ElementName = "sizeY")]
		public int SizeY { get; set; }

		[XmlElement(ElementName = "species")]
		public List<SeedBeltSpecies> Species { get; set; }

		public SeedBelt()
        {
			this.Width = 10;
			this.SizeX = 0;
			this.SizeY = 0;

			this.Species = new List<SeedBeltSpecies>();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                if (reader.IsStartElement("species"))
                {
                    SeedBeltSpecies species = new SeedBeltSpecies();
                    species.ReadXml(reader);
                    this.Species.Add(species);
                }
                else
                {
                    throw new XmlException("Encountered unexpected attributes.");
                }
            }
            else if (reader.IsStartElement("seedBelt"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("enabled"))
            {
                this.Enabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("width"))
            {
                this.Width = reader.ReadElementContentAsInt();
            }
            else if (reader.IsStartElement("sizeX"))
            {
                this.SizeX = reader.ReadElementContentAsInt();
            }
            else if (reader.IsStartElement("sizeY"))
            {
                this.SizeY = reader.ReadElementContentAsInt();
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
