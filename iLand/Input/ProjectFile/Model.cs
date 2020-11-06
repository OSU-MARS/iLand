using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Model : XmlSerializable
    {
		public ModelSettings Settings { get; private set; }
		public Species Species { get; private set; }
		public World World { get; private set; }
		public Site Site { get; private set; }
		public Climate Climate { get; private set; }
		public Initialization Initialization { get; private set; }
		public Management Management { get; private set; }
		public Parameter Parameter { get; private set; }

		public Model()
        {
			this.Climate = new Climate();
			this.Initialization = new Initialization();
			this.Management = new Management();
			this.Parameter = new Parameter();
			this.Settings = new ModelSettings();
			this.Site = new Site();
			this.Species = new Species();
			this.World = new World();
        }

        protected override void ReadStartElement(XmlReader reader)
        {
            if (reader.AttributeCount != 0)
            {
                throw new XmlException("Encountered unexpected attributes.");
            }

            if (reader.IsStartElement("model"))
            {
                reader.Read();
            }
            else if (reader.IsStartElement("settings"))
            {
                this.Settings.ReadXml(reader);
            }
			else if (reader.IsStartElement("species"))
			{
				this.Species.ReadXml(reader);
			}
			else if (reader.IsStartElement("world"))
			{
				this.World.ReadXml(reader);
			}
			else if (reader.IsStartElement("site"))
			{
				this.Site.ReadXml(reader);
			}
			else if (reader.IsStartElement("climate"))
			{
				this.Climate.ReadXml(reader);
			}
			else if (reader.IsStartElement("initialization"))
			{
				this.Initialization.ReadXml(reader);
			}
			else if (reader.IsStartElement("management"))
			{
				this.Management.ReadXml(reader);
			}
			else if (reader.IsStartElement("parameter"))
			{
				this.Parameter.ReadXml(reader);
			}
			else
			{
                throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
            }
        }
    }
}
