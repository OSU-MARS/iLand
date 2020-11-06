using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Outputs : XmlSerializable
    {
		public DynamicOutput Dynamic { get; private set; }
		public FilterOutput Tree { get; private set; }
		public FilterOutput TreeRemoved { get; private set; }
		public ConditionOutput Stand { get; private set; }
		public Enablable StandDead { get; private set; }
		public Enablable ProductionMonth { get; private set; }
		public Enablable Management { get; private set; }
		public ConditionOutput Sapling { get; private set; }
		public SaplingDetailOutput SaplingDetail { get; private set; }
		public ResourceUnitConditionOutput Carbon { get; private set; }
		public ResourceUnitConditionOutput CarbonFlow { get; private set; }
		public ResourceUnitConditionOutput Water { get; private set; }
		public ConditionOutput Landscape { get; private set; }
		public DynamicStandOutput DynamicStand { get; private set; }
		public Enablable BarkBeetle { get; private set; }
		public Enablable Wind { get; private set; }
		public Enablable Fire { get; private set; }
		public LandscapeRemovedOutput LandscapeRemoved { get; private set; }

		public Outputs()
        {
			this.Dynamic = new DynamicOutput();
			this.Tree = new FilterOutput();
			this.TreeRemoved = new FilterOutput();
			this.Stand = new ConditionOutput();
			this.StandDead = new Enablable();
			this.ProductionMonth = new Enablable();
			this.Management = new Enablable();
			this.Sapling = new ConditionOutput();
			this.SaplingDetail = new SaplingDetailOutput();
			this.Carbon = new ResourceUnitConditionOutput();
			this.CarbonFlow = new ResourceUnitConditionOutput();
			this.Water = new ResourceUnitConditionOutput();
			this.Landscape = new ConditionOutput();
			this.DynamicStand = new DynamicStandOutput();
			this.BarkBeetle = new Enablable();
			this.Wind = new Enablable();
			this.Fire = new Enablable();
			this.LandscapeRemoved = new LandscapeRemovedOutput();
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("output"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("dynamic"))
			{
				this.Dynamic.ReadXml(reader);
			}
			else if (reader.IsStartElement("tree"))
			{
				this.Tree.ReadXml(reader);
			}
			else if (reader.IsStartElement("treeremoved"))
			{
				this.TreeRemoved.ReadXml(reader);
			}
			else if (reader.IsStartElement("stand"))
			{
				this.Stand.ReadXml(reader);
			}
			else if (reader.IsStartElement("standdead"))
			{
				this.StandDead.ReadXml(reader);
			}
			else if (reader.IsStartElement("production_month"))
			{
				this.ProductionMonth.ReadXml(reader);
			}
			else if (reader.IsStartElement("management"))
			{
				this.Management.ReadXml(reader);
			}
			else if (reader.IsStartElement("sapling"))
			{
				this.Sapling.ReadXml(reader);
			}
			else if (reader.IsStartElement("saplingdetail"))
			{
				this.SaplingDetail.ReadXml(reader);
			}
			else if (reader.IsStartElement("carbon"))
			{
				this.Carbon.ReadXml(reader);
			}
			else if (reader.IsStartElement("carbonflow"))
			{
				this.CarbonFlow.ReadXml(reader);
			}
			else if (reader.IsStartElement("water"))
			{
				this.Water.ReadXml(reader);
			}
			else if (reader.IsStartElement("landscape"))
			{
				this.Landscape.ReadXml(reader);
			}
			else if (reader.IsStartElement("dynamicstand"))
			{
				this.DynamicStand.ReadXml(reader);
			}
			else if (reader.IsStartElement("barkbeetle"))
			{
				this.BarkBeetle.ReadXml(reader);
			}
			else if (reader.IsStartElement("wind"))
			{
				this.Wind.ReadXml(reader);
			}
			else if (reader.IsStartElement("fire"))
			{
				this.Fire.ReadXml(reader);
			}
			else if (reader.IsStartElement("landscape_removed"))
			{
				this.LandscapeRemoved.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}
