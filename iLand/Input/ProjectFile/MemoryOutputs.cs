using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class MemoryOutputs : XmlSerializable
    {
		public Enablable StandStatistics { get; private set; }

		public MemoryOutputs()
		{
			this.StandStatistics = new Enablable("standStatistics");
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				switch (reader.Name)
				{
					case "standStatistics":
						this.StandStatistics.ReadXml(reader);
						break;
					default:
						throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else
			{
				switch (reader.Name)
				{
					case "memory":
						reader.Read();
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
		}
	}
}
