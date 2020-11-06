using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Modules : XmlSerializable
	{
		public Fire Fire { get; private set; }
		public Wind Wind { get; private set; }
		public BarkBeetle BarkBeetle { get; private set; }

		public Modules()
        {
			this.BarkBeetle = new BarkBeetle();
			this.Fire = new Fire();
			this.Wind = new Wind();
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("modules"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("fire"))
			{
				this.Fire.ReadXml(reader);
			}
			else if (reader.IsStartElement("wind"))
			{
				this.Wind.ReadXml(reader);
			}
			else if (reader.IsStartElement("barkbeetle"))
			{
				this.BarkBeetle.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
