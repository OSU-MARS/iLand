using iLand.tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace iLand.Test
{
    [TestClass]
    public class TestXmlHelper
    {
        private static XmlHelper xml;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            xml = new XmlHelper();
            xml.LoadFromFile(Path.Combine(testContext.TestDir, "UnitTests", "test.xml"));
        }

        [TestMethod]
        public void Dump()
        {
            string test = xml.Dump("test");
            string path = xml.Dump("path");
            string species = xml.Dump("species");
        }

        [TestMethod]
        public void FilePath()
        {
            // setup file paths...
            xml.Dump("path");
            GlobalSettings.Instance.SetupDirectories(xml.Node("path"), null);
        }

        [TestMethod]
        public void Traverse()
        {
            Assert.IsTrue(xml.Node("") == null); // top node
            Assert.IsTrue(xml.Node("test.block.a") == null); // traverse
            xml.SetCurrentNode("test.block");
            Assert.IsTrue(xml.Node(".b") == null);
            Assert.IsTrue(xml.Node(".b.c") == null);
            Assert.IsTrue(xml.Node("test.block.b.c") == null);
            Assert.IsTrue(xml.Node(".b[0]") == null);
            Assert.IsTrue(xml.Node(".b[0].d") == null);
            Assert.IsTrue(xml.Node("test.block[1].n[2].o") == null);

            Assert.IsTrue(xml.Value("test.block.b.c") == "c");
            Assert.IsTrue(xml.Value("test.block.b.c.nonexistent", "0") == "0");
            Assert.IsTrue(xml.Value("test.block[1].n[2].o") == "o");
        }
    }
}