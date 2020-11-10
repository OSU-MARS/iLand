using System;
using System.Diagnostics;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Logging : XmlSerializable
    {
		public bool Flush { get; private set; }
		public string? LogFile { get; private set; }
		public string? LogTarget { get; private set; } // "console" or "file"

		public Logging()
        {
			this.Flush = Trace.AutoFlush;
			this.LogFile = null;
			this.LogTarget = null;
        }

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("logging"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("flush"))
			{
				this.Flush = reader.ReadElementContentAsBoolean();
				if (Trace.AutoFlush != this.Flush)
				{
					throw new NotImplementedException("Project's logging flush setting does not match System.Diagnostics.Trace.Autoflush but coordinated configuration of this setting is not shared across projects.");
				}
			}
			else if (reader.IsStartElement("logFile"))
			{
				this.LogFile = reader.ReadElementContentAsString().Trim();
				if (String.IsNullOrEmpty(this.LogFile) == false)
				{
					// Trace.Listeners.Add();
					throw new NotImplementedException("Attachment of trace file listeners is not currently supported. As a workaround, consider specifying a listener in app.config.");
				}
			}
			else if (reader.IsStartElement("logTarget"))
			{
				this.LogTarget = reader.ReadElementContentAsString().Trim();
				if (String.IsNullOrEmpty(this.LogTarget) == false)
				{
					throw new NotImplementedException("Specification of a log target is not currently supported. As a workaround, consider specifying a listener in app.config.");
				}
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
