using System;
using System.IO;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Project : XmlSerializable
    {
		public System System { get; private set; }
        public Model Model { get; private set; }
		public Outputs Output { get; private set; }
		public Modules Modules { get; private set; }
		public User User { get; private set; }

		public Project(string xmlFilePath)
        {
			this.Model = new Model();
			this.Modules = new Modules();
			this.Output = new Outputs();
			this.System = new System(Path.GetDirectoryName(xmlFilePath)); // if home path is not specified, default it to the location of the project file
			this.User = new User();

			using FileStream stream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using XmlReader reader = XmlReader.Create(stream);
			reader.MoveToContent();
			this.ReadXml(reader);
		}

		public string GetFilePath(ProjectDirectory directory, string fileName)
        {
			if (directory == ProjectDirectory.Home)
            {
				return Path.Combine(this.System.Path.Home, fileName);
            }
			string directoryName = directory switch
			{
				ProjectDirectory.Database => this.System.Path.Database,
				ProjectDirectory.Gis => this.System.Path.Gis,
				ProjectDirectory.Init => this.System.Path.Init,
				ProjectDirectory.LightIntensityProfile => this.System.Path.LightIntensityProfile,
				ProjectDirectory.Log => this.System.Path.Log,
				ProjectDirectory.Output => this.System.Path.Output,
				ProjectDirectory.Script => this.System.Path.Script,
				ProjectDirectory.Temp => this.System.Path.Temp,
				_ => throw new NotSupportedException("Unhandled project directory " + directory + ".")
			};
			return Path.Combine(this.System.Path.Home, directoryName, fileName);
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes.");
			}

			if (reader.IsStartElement("project"))
			{
				reader.Read();
			}
			else if (reader.IsStartElement("system"))
			{
				this.System.ReadXml(reader);
			}
			else if (reader.IsStartElement("model"))
			{
				this.Model.ReadXml(reader);
			}
			else if (reader.IsStartElement("output"))
			{
				this.Output.ReadXml(reader);
			}
			else if (reader.IsStartElement("modules"))
			{
				this.Modules.ReadXml(reader);
			}
			else if (reader.IsStartElement("user"))
			{
				this.User.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Encountered unknown element '" + reader.Name + "'.");
			}
		}
	}
}