using System;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Outputs : XmlSerializable
	{
		public AnnualOutputs Annual { get; private init; }
		public MemoryOutputs Memory { get; private init; }
		public Logging Logging { get; private init; }

		public Outputs()
        {
			this.Annual = new AnnualOutputs();
			this.Logging = new Logging();
			this.Memory = new MemoryOutputs();
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			if (String.Equals(reader.Name, "output", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "annual", StringComparison.Ordinal))
			{
				this.Annual.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "logging", StringComparison.Ordinal))
			{
				this.Logging.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "memory", StringComparison.Ordinal))
			{
				this.Memory.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
