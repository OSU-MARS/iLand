using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class AnnualOutputs : XmlSerializable
    {
		public string? DatabaseFile { get; private set; }

		public ResourceUnitConditionOutput Carbon { get; private init; }
		public ResourceUnitConditionOutput CarbonFlow { get; private init; }
		public DynamicStandAnnualOutput DynamicStand { get; private init; }
		public ConditionAnnualOutput Landscape { get; private init; }
		public LandscapeRemovedOutput LandscapeRemoved { get; private init; }
		public Enablable Management { get; private init; }
		public Enablable ProductionMonth { get; private init; }
		public ConditionAnnualOutput Sapling { get; private init; }
		public SaplingDetailAnnualOutput SaplingDetail { get; private init; }
		public ConditionAnnualOutput Stand { get; private init; }
		public Enablable StandDead { get; private init; }
		public FilterAnnualOutput Tree { get; private init; }
		public FilterAnnualOutput TreeRemoved { get; private init; }
		public ResourceUnitConditionOutput Water { get; private init; }

		public Enablable BarkBeetle { get; private init; }
		public Enablable Wind { get; private init; }
		public Enablable Fire { get; private init; }

		public AnnualOutputs()
		{
			this.DatabaseFile = null;

			this.Carbon = new ResourceUnitConditionOutput("carbon");
			this.CarbonFlow = new ResourceUnitConditionOutput("carbonFlow");
			this.DynamicStand = new DynamicStandAnnualOutput();
			this.Landscape = new ConditionAnnualOutput("landscape");
			this.LandscapeRemoved = new LandscapeRemovedOutput();
			this.Management = new Enablable("management");
			this.ProductionMonth = new Enablable("productionMonth");
			this.Sapling = new ConditionAnnualOutput("sapling");
			this.SaplingDetail = new SaplingDetailAnnualOutput();
			this.Stand = new ConditionAnnualOutput("stand");
			this.StandDead = new Enablable("standDead");
			this.Tree = new FilterAnnualOutput("tree");
			this.TreeRemoved = new FilterAnnualOutput("treeRemoved");
			this.Water = new ResourceUnitConditionOutput("water");

			this.BarkBeetle = new Enablable("barkBeetle");
			this.Wind = new Enablable("wind");
			this.Fire = new Enablable("fire");
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				switch (reader.Name)
				{
					case "barkBeetle":
						this.BarkBeetle.ReadXml(reader);
						break;
					case "carbon":
						this.Carbon.ReadXml(reader);
						break;
					case "carbonFlow":
						this.CarbonFlow.ReadXml(reader);
						break;
					case "dynamicStand":
						this.DynamicStand.ReadXml(reader);
						break;
					case "fire":
						this.Fire.ReadXml(reader);
						break;
					case "landscape":
						this.Landscape.ReadXml(reader);
						break;
					case "landscapeRemoved":
						this.LandscapeRemoved.ReadXml(reader);
						break;
					case "productionMonth":
						this.ProductionMonth.ReadXml(reader);
						break;
					case "management":
						this.Management.ReadXml(reader);
						break;
					case "sapling":
						this.Sapling.ReadXml(reader);
						break;
					case "saplingDetail":
						this.SaplingDetail.ReadXml(reader);
						break;
					case "stand":
						this.Stand.ReadXml(reader);
						break;
					case "standDead":
						this.StandDead.ReadXml(reader);
						break;
					case "tree":
						this.Tree.ReadXml(reader);
						break;
					case "treeRemoved":
						this.TreeRemoved.ReadXml(reader);
						break;
					case "water":
						this.Water.ReadXml(reader);
						break;
					case "wind":
						this.Wind.ReadXml(reader);
						break;
					default:
						throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
				}
			}
			else
			{
				switch (reader.Name)
				{
					case "annual":
						reader.Read();
						break;
					case "databaseFile":
						this.DatabaseFile = reader.ReadElementContentAsString().Trim();
						break;
					default:
						throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
				}
			}
		}
	}
}
