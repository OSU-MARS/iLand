using iLand.Tools;
using System;
using System.IO;
using System.Xml.Serialization;

namespace iLand.Input.ProjectFile
{
    [XmlRoot(ElementName = "project")]
    public class Project
    {
        [XmlElement(ElementName = "system")]
		public System System { get; set; }

		[XmlElement(ElementName = "model")]
        public Model Model { get; set; }

		[XmlElement(ElementName = "output")]
		public Outputs Output { get; set; }

		[XmlElement(ElementName = "modules")]
		public Modules Modules { get; set; }

		[XmlElement(ElementName = "user")]
		public User User { get; set; }

		public Project()
        {
			this.Model = new Model();
			this.Modules = new Modules();
			this.Output = new Outputs();
			this.System = new System();
			this.User = new User();
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

		public static Project Load(string xmlFilePath)
		{
			// parse project file
			XmlConformingSerializer deserializer = new XmlConformingSerializer(typeof(Project));
			using FileStream stream = new FileStream(xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			Project project = deserializer.Deserialize<Project>(stream);

			// if home path is not specified, default it to the location of the project file
			if (String.IsNullOrEmpty(project.System.Path.Home))
            {
				project.System.Path.Home = Path.GetDirectoryName(xmlFilePath);
            }
			return project;
		}
	}
}