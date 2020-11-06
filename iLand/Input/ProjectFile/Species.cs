using System.Collections.Generic;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Species : XmlSerializable
    {
        public string Source { get; private set; }
        public string ReaderStampFile { get; private set; }
        public NitrogenResponseClasses NitrogenResponseClasses { get; private set; }
        public CO2Response CO2Response { get; private set; }
        public LightResponse LightResponse { get; private set; }
        public List<PhenologyType> Phenology { get; private set; }

        public Species()
        {
            this.CO2Response = new CO2Response();
            this.LightResponse = new LightResponse();
            this.NitrogenResponseClasses = new NitrogenResponseClasses();
            this.ReaderStampFile = "readerstamp.bin";
            // this.Source;

            this.Phenology = new List<PhenologyType>();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("species"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("source"))
			{
				this.Source = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("reader"))
			{
				this.ReaderStampFile = reader.ReadElementContentAsString().Trim();
			}
			else if (reader.IsStartElement("nitrogenResponseClasses"))
			{
				this.NitrogenResponseClasses.ReadXml(reader);
			}
            else if (reader.IsStartElement("CO2Response"))
            {
                this.CO2Response.ReadXml(reader);
            }
            else if (reader.IsStartElement("lightResponse"))
            {
                this.LightResponse.ReadXml(reader);
            }
            else if (reader.IsStartElement("phenology"))
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
                                throw new XmlException("Encountered unexpected attributes.");
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
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
