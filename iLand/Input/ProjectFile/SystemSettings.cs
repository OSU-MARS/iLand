using System;
using System.Diagnostics.Tracing;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class SystemSettings : XmlSerializable
    {
        public bool Multithreading { get; private set; }
        public bool DebugOutput { get; private set; }
        public bool DebugOutputAutoSave { get; private set; }
        public Nullable<int> RandomSeed { get; private set; }

        // linearization of expressions: if true *and* linearize() is explicitely called, then
        // function results will be cached over a defined range of values.
        public bool ExpressionLinearizationEnabled { get; private set; }

        public EventLevel LogLevel { get; private set; } // TODO: change to enum

        public SystemSettings()
        {
            this.ExpressionLinearizationEnabled = false;
            this.LogLevel = EventLevel.Informational;
            this.Multithreading = false;
            this.RandomSeed = null;
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("settings"))
            {
                reader.Read();
                return;
            }
            else if (reader.IsStartElement("multithreading"))
            {
                this.Multithreading = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("debugOutput"))
            {
                this.DebugOutput = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("debugOutputAutoSave"))
            {
                this.DebugOutputAutoSave = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("randomSeed"))
            {
                this.RandomSeed = reader.ReadElementContentAsInt();
            }
            else if (reader.IsStartElement("expressionLinearizationEnabled"))
            {
                this.ExpressionLinearizationEnabled = reader.ReadElementContentAsBoolean();
            }
            else if (reader.IsStartElement("logLevel"))
            {
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
            }
            else
            {
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
