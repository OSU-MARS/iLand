using iLand.Core;
using iLand.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace iLand.Test
{
    public class LandTest
    {
        protected string GetDefaultProjectPath(TestContext testContext)
        {
            return Path.Combine(testContext.TestDir, "..", "..", "UnitTests", "testProject", "testProject.xml");
        }

        protected Model LoadProject(string projectFilePath)
        {
            // see also ModelController
            Model model = new Model();
            model.GlobalSettings.LoadProjectFile(projectFilePath);
            model.GlobalSettings.CurrentYear = 1;
            model.LoadProject();
            model.BeforeRun();
            return model;
        }
    }
}
