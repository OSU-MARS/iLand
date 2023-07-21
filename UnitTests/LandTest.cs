using iLand.Input.ProjectFile;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using System.IO;
using Model = iLand.Simulation.Model;

namespace iLand.Test
{
    public class LandTest
    {
        protected static string GetElliottProjectPath(TestContext testContext)
        {
            return Path.Combine(LandTest.GetUnitTestDirectoryPath(testContext), "Elliott", "Elliott.xml");
        }

        protected static string GetKalkalpenProjectPath(TestContext testContext)
        {
            return Path.Combine(LandTest.GetUnitTestDirectoryPath(testContext), "Kalkalpen", "Kalkalpen.xml");
        }

        protected static string GetMalcolmKnappProjectPath(string projectFileName)
        {
            return Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "OSU", "iLand", "Malcolm Knapp", projectFileName);
        }

        protected static string GetUnitTestDirectoryPath(TestContext testContext)
        {
            Debug.Assert(string.IsNullOrWhiteSpace(testContext.TestRunDirectory) == false);
            return Path.Combine(testContext.TestRunDirectory, "..", "..", "UnitTests");
        }

        protected static Model LoadProject(string projectFilePath)
        {
            Project projectFile = new(projectFilePath);
            Model model = new(projectFile);
            return model;
        }
    }
}
