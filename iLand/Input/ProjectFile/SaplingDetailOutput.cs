using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class SaplingDetailOutput : ConditionOutput
    {
        [XmlElement(ElementName = "minDbh")]
        public double MinDbh { get; set; }

        public SaplingDetailOutput()
        {
            this.MinDbh = 0.0;
        }
    }
}
