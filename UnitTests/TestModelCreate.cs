using iLand.core;
using iLand.tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace iLand.Test
{
    [TestClass]
    public class TestModelCreate
    {
        private static Model model;

        public TestContext TestContext { get; set; }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            model = new Model();
            GlobalSettings.Instance.LoadProjectFile(Path.Combine(testContext.TestDir, "..", "..", "UnitTests", "testProject", "testProject.xml"));
            model.LoadProject();
        }

        [TestMethod]
        public void SpeciesFormula()
        {
            Species species = model.FirstResourceUnit().SpeciesSet.GetSpecies("piab");
            Assert.IsTrue(species != null);
            // equation: m = 1.2*d^1.5
            Assert.IsTrue(Math.Round(species.GetBiomassFoliage(2)) == 3.0);
            Assert.IsTrue(Math.Round(species.GetBiomassFoliage(20)) == 107.0);
            Assert.IsTrue(Math.Round(species.GetBiomassFoliage(50)) == 424.0);
            Assert.IsTrue(Math.Round(species.GetBiomassFoliage(100)) == 1200.0);

            double x = species.GetBiomassFoliage(56.0);
            Assert.IsTrue(x >= 0.0);
        }
    }
}
