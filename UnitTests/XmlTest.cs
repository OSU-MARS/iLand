using iLand.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace iLand.Test
{
    [TestClass]
    public class XmlTest
    {
        private static XmlHelper Xml;
        private static string XmlFilePath;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            XmlTest.XmlFilePath = Path.Combine(testContext.TestDir, "..", "..", "UnitTests", "xmlHelpertest.xml");
            XmlTest.Xml = new XmlHelper();
            XmlTest.Xml.LoadFromFile(XmlTest.XmlFilePath);
        }

        [TestMethod]
        public void DirectoryPaths()
        {
            // string path = xml.Dump("path");
            GlobalSettings.Instance.SetupDirectories(XmlTest.Xml.Node("path"), XmlTest.XmlFilePath);

            string home = GlobalSettings.Instance.Path(null, "home");
            string database = GlobalSettings.Instance.Path("database");
            string lip = GlobalSettings.Instance.Path("lip");
            string temp = GlobalSettings.Instance.Path("temp");

            Assert.IsTrue(String.IsNullOrWhiteSpace(home) == false);
            Assert.IsTrue(String.IsNullOrWhiteSpace(database) == false);
            Assert.IsTrue(String.IsNullOrWhiteSpace(lip) == false);
            Assert.IsTrue(String.IsNullOrWhiteSpace(temp) == false);

            Assert.IsTrue(String.Equals(database, Path.Combine(home, "database"), StringComparison.Ordinal));
            Assert.IsTrue(String.Equals(lip, Path.Combine(home, "lip"), StringComparison.Ordinal));
            Assert.IsTrue(String.Equals(temp, Path.Combine(home, "temp"), StringComparison.Ordinal));
        }

        [TestMethod]
        public void Dump()
        {
            string test = XmlTest.Xml.Dump("test");
            string path = XmlTest.Xml.Dump("path");
            string species = XmlTest.Xml.Dump("species");

            Assert.IsTrue(String.IsNullOrWhiteSpace(test) == false);
            Assert.IsTrue(String.IsNullOrWhiteSpace(path) == false);
            Assert.IsTrue(String.IsNullOrWhiteSpace(species) == false);

            Assert.IsTrue(test.StartsWith("test.block[0].a[0]", StringComparison.Ordinal));
            Assert.IsTrue(path.StartsWith("path.home[0]", StringComparison.Ordinal));
            Assert.IsTrue(species.StartsWith("species.source[0]", StringComparison.Ordinal));
        }

        [TestMethod]
        public void Traverse()
        {
            XmlHelper xml = new XmlHelper();
            xml.LoadFromFile(XmlTest.XmlFilePath);

            Assert.IsTrue(Object.ReferenceEquals(xml.Node(""), xml.TopNode)); // top node
            Assert.IsTrue(String.Equals(xml.Node("test.block.a").Name, "a", StringComparison.Ordinal)); // traverse

            xml.TrySetCurrentNode("test.block");
            Assert.IsTrue(String.Equals(xml.Node(".b").Name, "b", StringComparison.Ordinal));
            Assert.IsTrue(String.Equals(xml.Node(".b.c").Name, "c", StringComparison.Ordinal));
            Assert.IsTrue(String.Equals(xml.Node("test.block.b.c").Name, "c", StringComparison.Ordinal));
            Assert.IsTrue(String.Equals(xml.Node(".b[0]").Name, "b", StringComparison.Ordinal));
            Assert.IsTrue(String.Equals(xml.Node(".b[0].d").Name, "d", StringComparison.Ordinal));
            Assert.IsTrue(String.Equals(xml.Node("test.block[1].n[2].o").Name, "o", StringComparison.Ordinal));

            Assert.IsTrue(xml.GetString("test.block.b.c") == "c");
            Assert.IsTrue(xml.GetString("test.block.b.c.nonexistent", "0") == "0");
            Assert.IsTrue(xml.GetString("test.block[1].n[2].o") == "o");
        }
    }
}