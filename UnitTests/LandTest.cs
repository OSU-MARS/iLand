using iLand.Core;
using iLand.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace iLand.Test
{
    public class LandTest
    {
        private static readonly object LockObject = new object();

        protected static string ProjectFilePath { get; set; }
        protected static Model Model { get; private set; }

        protected static void EnsureModel(TestContext testContext)
        {
            if (LandTest.Model == null)
            {
                lock (LandTest.LockObject)
                {
                    if (LandTest.Model == null)
                    {
                        // see also ModelControlller.
                        LandTest.ProjectFilePath = Path.Combine(testContext.TestDir, "..", "..", "UnitTests", "testProject", "testProject.xml");
                        LandTest.Model = new Model();
                        GlobalSettings.Instance.LoadProjectFile(LandTest.ProjectFilePath);
                        GlobalSettings.Instance.CurrentYear = 1;
                        LandTest.Model.LoadProject();
                        LandTest.Model.BeforeRun();
                    }
                }
            }
        }
    }
}
