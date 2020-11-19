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
				if (String.Equals(reader.Name, "standStatistics", StringComparison.Ordinal))
				{
					this.StandStatistics.ReadXml(reader);
				}
				else
				{
					throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else if (String.Equals(reader.Name, "memory", StringComparison.Ordinal))
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
