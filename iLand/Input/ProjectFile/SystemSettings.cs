using System;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class SystemSettings
    {
        [XmlElement(ElementName = "multithreading")]
        public bool Multithreading { get; set; }

        [XmlElement(ElementName = "debugOutput")]
        public bool DebugOutput { get; set; }

        [XmlElement(ElementName = "debugOutputAutoSave")]
        public bool DebugOutputAutoSave { get; set; }

        [XmlElement(ElementName = "randomSeed")]
        public Nullable<int> RandomSeed { get; set; }

        // linearization of expressions: if true *and* linearize() is explicitely called, then
        // function results will be cached over a defined range of values.
        [XmlElement(ElementName = "expressionLinearizationEnabled")]
        public bool ExpressionLinearizationEnabled { get; set; }

        [XmlElement(ElementName = "logLevel")]
        public string LogLevel { get; set; }

        public SystemSettings()
        {
            this.ExpressionLinearizationEnabled = false;
            this.LogLevel = "debug";
            this.Multithreading = false;
            this.RandomSeed = null;
        }
    }
}
