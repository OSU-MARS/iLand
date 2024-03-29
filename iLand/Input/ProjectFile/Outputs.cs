﻿using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Outputs : XmlSerializable
	{
		public MemoryOutputs Memory { get; private init; }
		public Logging Logging { get; private init; }
		public SqlOutputs Sql { get; private init; }

		public Outputs()
        {
			this.Logging = new();
			this.Memory = new();
			this.Sql = new();
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			switch (reader.Name)
			{
				case "output":
					reader.Read();
					break;
				case "logging":
					this.Logging.ReadXml(reader);
					break;
				case "memory":
					this.Memory.ReadXml(reader);
					break;
				case "sql":
					this.Sql.ReadXml(reader);
					break;
				default:
					throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
