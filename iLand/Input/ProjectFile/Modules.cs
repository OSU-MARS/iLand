using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Modules : XmlSerializable
	{
		public Fire Fire { get; init; }
		public Wind Wind { get; init; }
		public BarkBeetle BarkBeetle { get; init; }

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

			if (String.Equals(reader.Name, "modules", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "fire", StringComparison.Ordinal))
			{
				this.Fire.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "wind", StringComparison.Ordinal))
			{
				this.Wind.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "barkbeetle", StringComparison.Ordinal))
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
