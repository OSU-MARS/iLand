using System.Collections.Generic;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Species : XmlSerializable
    {
        public string? DatabaseFile { get; private set; }
        public string? DatabaseTable { get; private set; }

        public NitrogenResponseClasses NitrogenResponseClasses { get; private init; }
        public CO2Response CO2Response { get; private init; }
        public LightResponse LightResponse { get; private init; }
        public List<LeafPhenology> Phenology { get; private init; }

        public Species()
        {
            this.DatabaseFile = null;
            this.DatabaseTable = Constant.Data.DefaultSpeciesTable;

            this.CO2Response = new();
            this.LightResponse = new();
            this.NitrogenResponseClasses = new();
            this.Phenology = [];
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

            switch (reader.Name)
            {
                case "species":
                    reader.Read();
                    break;
                case "databaseFile":
                    this.DatabaseFile = reader.ReadElementContentAsString().Trim();
                    break;
                case "databaseTable":
                    this.DatabaseTable = reader.ReadElementContentAsString().Trim();
                    break;
                case "nitrogenResponseClasses":
                    this.NitrogenResponseClasses.ReadXml(reader);
                    break;
                case "co2response":
                    this.CO2Response.ReadXml(reader);
                    break;
                case "lightResponse":
                    this.LightResponse.ReadXml(reader);
                    break;
                case "phenology":
                    XmlReader phenologyReader = reader.ReadSubtree();
                    phenologyReader.MoveToContent();
                    while (phenologyReader.EOF == false)
                    {
                        if (phenologyReader.IsStartElement())
                        {
                            if (phenologyReader.IsStartElement("phenology"))
                            {
                                if (reader.AttributeCount != 0)
                                {
                                    throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
                                }
                                phenologyReader.Read();
                            }
                            else if (phenologyReader.IsStartElement("type"))
                            {
                                LeafPhenology phenology = new();
                                phenology.ReadXml(phenologyReader);
                                this.Phenology.Add(phenology);
                            }
                            else
                            {
                                throw new XmlException("Encountered unknown element '" + phenologyReader.Name + "'.");
                            }
                        }
                        else
                        {
                            phenologyReader.Read();
                        }
                    }
                    break;
                default:
                    throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
            }
		}
	}
}
