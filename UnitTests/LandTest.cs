using iLand.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

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
            // see also ModelController
            Model model = new Model();
            model.LoadProject(projectFilePath);
            model.ModelSettings.CurrentYear = 1; // TODO: determine if this is needed
            model.BeforeRun();
            return model;
        }
    }
}
