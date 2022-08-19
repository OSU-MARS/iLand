using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Logging : XmlSerializable
    {
		public bool AutoFlush { get; private set; }
        public Enablable HeightGrid { get; private init; }
        public Enablable LightGrid { get; private init; }
		public string? LogFile { get; private set; }
		public EventLevel LogLevel { get; private set; }

		public Logging()
        {
			this.AutoFlush = Trace.AutoFlush;
			this.HeightGrid = new("heightGrid");
            this.LightGrid = new("lightGrid");
            this.LogFile = null;
			this.LogLevel = EventLevel.Warning;
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				switch (reader.Name)
				{
					case "heightGrid":
						this.HeightGrid.ReadXml(reader);
						break;
                    case "lightGrid":
                        this.LightGrid.ReadXml(reader);
                        break;
                    default:
						throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else
			{
				switch (reader.Name)
				{
					case "logging":
						reader.Read();
						break;
					case "autoFlush":
						this.AutoFlush = reader.ReadElementContentAsBoolean();
						break;
					case "logFile":
						this.LogFile = reader.ReadElementContentAsString().Trim();
						if (String.IsNullOrWhiteSpace(this.LogFile))
						{
							throw new XmlException("logFile element is present but does not contain a file name.");
						}
						break;
					case "logLevel":
						string logLevel = reader.ReadElementContentAsString().Trim();
						this.LogLevel = Enum.Parse<EventLevel>(logLevel, ignoreCase: true);
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
		}
	}
}
