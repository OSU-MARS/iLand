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
				switch (reader.Name)
				{
					case "fire":
						this.Fire.ReadXml(reader);
						break;
					case "wind":
						this.Wind.ReadXml(reader);
						break;
					case "barkBeetle":
						this.BarkBeetle.ReadXml(reader);
						break;
					default:
						throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else
			{
				switch (reader.Name)
				{
					case "modules":
						reader.Read();
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
		}
	}
}
