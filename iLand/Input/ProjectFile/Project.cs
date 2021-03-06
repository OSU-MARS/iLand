﻿using System;
using System.IO;
using System.Xml;

namespace iLand.Input.ProjectFile
{
    public class Project : XmlSerializable
    {
		public System System { get; private init; }
        public Model Model { get; private init; }
		public Outputs Output { get; private init; }
		public Modules Modules { get; private init; }
		public User User { get; private init; }
		public World World { get; private init; }

		public Project(string xmlFilePath)
        {
			this.Model = new Model();
			this.Modules = new Modules();
			this.Output = new Outputs();
			this.System = new System(Path.GetDirectoryName(xmlFilePath)); // if home path is not specified, default it to the location of the project file
			this.User = new User();
			this.World = new World();

			using FileStream stream = new(xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using XmlReader reader = XmlReader.Create(stream);
			reader.MoveToContent();
			this.ReadXml(reader);
		}

		public string GetFilePath(ProjectDirectory directory, string? fileName)
        {
			if (String.IsNullOrEmpty(fileName))
            {
				throw new ArgumentNullException(nameof(fileName));
            }

			if (directory == ProjectDirectory.Home)
            {
				return Path.Combine(this.System.Paths.Home, fileName);
            }
			string directoryName = directory switch
			{
				ProjectDirectory.Database => this.System.Paths.Database,
				ProjectDirectory.Gis => this.System.Paths.Gis,
				ProjectDirectory.Init => this.System.Paths.Init,
				ProjectDirectory.LightIntensityProfile => this.System.Paths.LightIntensityProfile,
				ProjectDirectory.Log => this.System.Paths.Log,
				ProjectDirectory.Output => this.System.Paths.Output,
				ProjectDirectory.Script => this.System.Paths.Script,
				ProjectDirectory.Temp => this.System.Paths.Temp,
				_ => throw new NotSupportedException("Unhandled project directory " + directory + ".")
			};
			return Path.Combine(this.System.Paths.Home, directoryName, fileName);
		}

		protected override void ReadStartElement(XmlReader reader)
		{
			if (reader.AttributeCount != 0)
			{
				throw new XmlException("Encountered unexpected attributes on element " + reader.Name + ".");
			}

			if (String.Equals(reader.Name, "project", StringComparison.Ordinal))
			{
				reader.Read();
			}
			else if (String.Equals(reader.Name, "system", StringComparison.Ordinal))
			{
				this.System.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "model", StringComparison.Ordinal))
			{
				this.Model.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "output", StringComparison.Ordinal))
			{
				this.Output.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "modules", StringComparison.Ordinal))
			{
				this.Modules.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "user", StringComparison.Ordinal))
			{
				this.User.ReadXml(reader);
			}
			else if (String.Equals(reader.Name, "world", StringComparison.Ordinal))
			{
				this.World.ReadXml(reader);
			}
			else
			{
				throw new XmlException("Element '" + reader.Name + "' is unknown, has unexpected attributes, or is missing expected attributes.");
			}
		}
	}
}