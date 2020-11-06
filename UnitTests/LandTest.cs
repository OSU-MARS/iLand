using iLand.Input.ProjectFile;
using iLand.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Model = iLand.Simulation.Model;

namespace iLand.Test
{
    public class LandTest
    {
        protected string GetKalkalpenProjectPath(TestContext testContext)
        {
            return Path.Combine(testContext.TestDir, "..", "..", "UnitTests", "testProject", "testProject.xml");
        }

        protected string GetMalcolmKnappProjectPath(string projectFileName)
        {
            return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "OSU", "iLand", "Malcolm Knapp", projectFileName);
        }

        protected Model LoadProject(string projectFilePath)
        {
            Project projectFile = new Project(projectFilePath);
            Landscape landscape = new Landscape(projectFile);

            Model model = new Model(projectFile, landscape);
            model.Setup();
            return model;
        }
    }
}
