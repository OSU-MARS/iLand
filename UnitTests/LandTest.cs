using iLand.Input.ProjectFile;
using iLand.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Model = iLand.Simulation.Model;

namespace iLand.Test
{
    public class LandTest
    {
        private static string GetTestProjectDirectoryPath(TestContext testContext)
        {
            return Path.Combine(testContext.TestDir, "..", "..", "UnitTests", "testProject");
        }

        protected static string GetKalkalpenProjectPath(TestContext testContext)
        {
            return Path.Combine(LandTest.GetTestProjectDirectoryPath(testContext), "kalkalpen.xml");
        }

        protected static string GetMalcolmKnappProjectPath(string projectFileName)
        {
            return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "OSU", "iLand", "Malcolm Knapp", projectFileName);
        }

        protected static string GetPacificNorthwestProjectPath(TestContext testContext)
        {
            return Path.Combine(LandTest.GetTestProjectDirectoryPath(testContext), "pacificNorthwest.xml");
        }

        protected static Model LoadProject(string projectFilePath)
        {
            Project projectFile = new Project(projectFilePath);
            Landscape landscape = new Landscape(projectFile);

            Model model = new Model(projectFile, landscape);
            model.Setup();
            return model;
        }
    }
}
