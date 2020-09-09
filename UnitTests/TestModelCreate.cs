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
        public void initTestCase()
        {
            //XmlHelper xml("E:\\dev\\iland\\src\\tests\\modelCreate\\test.xml");
            model = new Model();
            GlobalSettings.instance().loadProjectFile(Path.Combine(this.TestContext.TestDir, "UnitTests", "test.xml"));
            model.loadProject();
        }

        [TestMethod]
        public void speciesFormula()
        {
            Species species = model.ru().speciesSet().species("piab");
            Assert.IsTrue(species != null);
            // equation: m = 1.2*d^1.5
            Assert.IsTrue(Math.Round(species.biomassFoliage(2)) == 3.0);
            Assert.IsTrue(Math.Round(species.biomassFoliage(20)) == 107.0);
            Assert.IsTrue(Math.Round(species.biomassFoliage(50)) == 424.0);
            Assert.IsTrue(Math.Round(species.biomassFoliage(100)) == 1200.0);

            double x = species.biomassFoliage(56.0);
        }
    }
}
