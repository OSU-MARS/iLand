using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Parameter
    {
        // TODO: move to world
        // special mode that treats each resource unit as a "torus" (light calculation, seed distribution)
        [XmlElement(ElementName = "torus")]
        public bool Torus { get; set; }

        [XmlElement(ElementName = "debug_tree")]
        public string DebugTree { get; set; }

        [XmlElement(ElementName = "debug_clear")]
        public bool DebugClear { get; set; }

        [XmlElement(ElementName = "gpp_per_year")]
        public float GppPerYear { get; set; }

        [XmlElement(ElementName = "debugDumpStamps")]
        public bool DebugDumpStamps { get; set; }

        public Parameter()
        {
            this.DebugDumpStamps = false;
            this.DebugTree = null;
            this.GppPerYear = 0.0F;
            this.Torus = false;
        }
    }
}
