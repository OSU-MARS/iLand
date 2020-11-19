using System;
using System.Collections.Generic;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Species : XmlSerializable
    {
        public string? DatabaseFile { get; private set; }
        public string? DatabaseTable { get; private set; }
        public string ReaderStampFile { get; private set; }

        public NitrogenResponseClasses NitrogenResponseClasses { get; private init; }
        public CO2Response CO2Response { get; private init; }
        public LightResponse LightResponse { get; private init; }
        public List<PhenologyType> Phenology { get; private init; }

        public Species()
        {
            this.DatabaseFile = null;
            this.DatabaseTable = null;
            this.ReaderStampFile = "readerstamp.bin";

            this.CO2Response = new CO2Response();
            this.LightResponse = new LightResponse();
            this.NitrogenResponseClasses = new NitrogenResponseClasses();
            this.Phenology = new List<PhenologyType>();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			if (String.Equals(reader.Name, "species", StringComparison.Ordinal))
			{
				reader.Read();
			}
            else if (String.Equals(reader.Name, "databaseFile", StringComparison.Ordinal))
            {
                this.DatabaseFile = reader.ReadElementContentAsString().Trim();
            }
            else if (String.Equals(reader.Name, "databaseTable", StringComparison.Ordinal))
			{
				this.DatabaseTable = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "reader", StringComparison.Ordinal))
			{
				this.ReaderStampFile = reader.ReadElementContentAsString().Trim();
			}
			else if (String.Equals(reader.Name, "nitrogenResponseClasses", StringComparison.Ordinal))
			{
				this.NitrogenResponseClasses.ReadXml(reader);
			}
            else if (String.Equals(reader.Name, "co2response", StringComparison.Ordinal))
            {
                this.CO2Response.ReadXml(reader);
            }
            else if (String.Equals(reader.Name, "lightResponse", StringComparison.Ordinal))
            {
                this.LightResponse.ReadXml(reader);
            }
            else if (String.Equals(reader.Name, "phenology", StringComparison.Ordinal))
            {
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
                            PhenologyType phenology = new PhenologyType();
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
            }
            else
            {
				throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
