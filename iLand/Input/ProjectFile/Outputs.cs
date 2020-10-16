using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    public class Outputs
    {
		[XmlElement(ElementName = "dynamic")]
		public DynamicOutput Dynamic { get; set; }

		[XmlElement(ElementName = "tree")]
		public FilterOutput Tree { get; set; }

		[XmlElement(ElementName = "treeremoved")]
		public FilterOutput TreeRemoved { get; set; }

		[XmlElement(ElementName = "stand")]
		public ConditionOutput Stand { get; set; }

		[XmlElement(ElementName = "standdead")]
		public Enablable StandDead { get; set; }

		[XmlElement(ElementName = "production_month")]
		public Enablable ProductionMonth { get; set; }

		[XmlElement(ElementName = "management")]
		public Enablable Management { get; set; }

		[XmlElement(ElementName = "sapling")]
		public ConditionOutput Sapling { get; set; }

		[XmlElement(ElementName = "saplingdetail")]
		public SaplingDetailOutput SaplingDetail { get; set; }

		[XmlElement(ElementName = "carbon")]
		public ResourceUnitConditionOutput Carbon { get; set; }

		[XmlElement(ElementName = "carbonflow")]
		public ResourceUnitConditionOutput CarbonFlow { get; set; }

		[XmlElement(ElementName = "water")]
		public ResourceUnitConditionOutput Water { get; set; }

		[XmlElement(ElementName = "landscape")]
		public ConditionOutput Landscape { get; set; }

		[XmlElement(ElementName = "dynamicstand")]
		public DynamicStandOutput DynamicStand { get; set; }

		[XmlElement(ElementName = "barkbeetle")]
		public Enablable BarkBeetle { get; set; }

		[XmlElement(ElementName = "wind")]
		public Enablable Wind { get; set; }

		[XmlElement(ElementName = "fire")]
		public Enablable Fire { get; set; }

		[XmlElement(ElementName = "landscape_removed")]
		public LandscapeRemovedOutput LandscapeRemoved { get; set; }
    }
}
