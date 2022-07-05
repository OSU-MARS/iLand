using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Logging : XmlSerializable
    {
		//public bool DebugOutput { get; private set; }
		// 1=Tree NPP, 2=Tree partition, 4=tree growth, 8=Standlevel NPP, 16=Water Cycle, 32=Daily responses, 64=Establishment, 128=Carbon Cycle, Performance = 256
		//public bool DebugOutputAutoSave { get; private set; }
		public bool Flush { get; private set; }
		public string? LogFile { get; private set; }
		public EventLevel LogLevel { get; private set; }

		public Logging()
        {
			this.Flush = Trace.AutoFlush;
			this.LogFile = null;
			this.LogLevel = EventLevel.Warning;
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			switch (reader.Name)
			{
				case "logging":
					reader.Read();
					break;
				case "flush":
					this.Flush = reader.ReadElementContentAsBoolean();
					if (Trace.AutoFlush != this.Flush)
					{
						throw new NotImplementedException("Project's logging flush setting does not match System.Diagnostics.Trace.Autoflush but coordinated configuration of this setting is not shared across projects.");
					}
					break;
				case "logFile":
					this.LogFile = reader.ReadElementContentAsString().Trim();
					if (String.IsNullOrEmpty(this.LogFile) == false)
					{
						// Trace.Listeners.Add();
						throw new NotImplementedException("Attachment of trace file listeners is not currently supported. As a workaround, consider specifying a listener in app.config.");
					}
					break;
				case "logLevel":
					string logLevelAsString = reader.ReadElementContentAsString().Trim();
					this.LogLevel = logLevelAsString switch
					{
						"critical" => EventLevel.Critical,
						"error" => EventLevel.Error,
						"informational" => EventLevel.Informational,
						"logAlways" => EventLevel.LogAlways,
						"verbose" => EventLevel.Verbose,
						"warning" => EventLevel.Warning,
						_ => throw new NotSupportedException("Unhandled log level '" + logLevelAsString + "'.")
					};
					break;
				default:
					throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}
