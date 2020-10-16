using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class AreaMask : Enablable
    {
        [XmlElement(ElementName = "imageFile")]
        public string ImageFile { get; set; }

        public AreaMask()
        {
            this.ImageFile = null;
        }
    }
}
