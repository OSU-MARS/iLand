using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Modules : XmlSerializable
	{
		public Fire Fire { get; private init; }
		public Wind Wind { get; private init; }
		public BarkBeetle BarkBeetle { get; private init; }

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
				if (String.Equals(reader.Name, "fire", StringComparison.Ordinal))
				{
					this.Fire.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "wind", StringComparison.Ordinal))
				{
					this.Wind.ReadXml(reader);
				}
				else if (String.Equals(reader.Name, "barkBeetle", StringComparison.Ordinal))
				{
					this.BarkBeetle.ReadXml(reader);
				}
				else
				{
					throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else if (String.Equals(reader.Name, "modules", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else
			{
				throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
