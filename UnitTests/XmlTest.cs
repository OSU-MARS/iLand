using iLand.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace iLand.Test
{
    [TestClass]
    public class XmlTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void DirectoryPaths()
        {
            // string path = xml.Dump("path");
            GlobalSettings globalSettings = new GlobalSettings();
            XmlHelper xml = this.LoadXml();
            globalSettings.SetupDirectories(xml.Node("path"), this.GetXmlFilePath());

            string home = globalSettings.Path(null, "home");
            string database = globalSettings.Path("database");
            string lip = globalSettings.Path("lip");
            string temp = globalSettings.Path("temp");

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
            XmlHelper xml = this.LoadXml();
            string test = xml.Dump("test");
            string path = xml.Dump("path");
            string species = xml.Dump("species");

            Assert.IsTrue(String.IsNullOrWhiteSpace(test) == false);
            Assert.IsTrue(String.IsNullOrWhiteSpace(path) == false);
            Assert.IsTrue(String.IsNullOrWhiteSpace(species) == false);

            Assert.IsTrue(test.StartsWith("test.block[0].a[0]", StringComparison.Ordinal));
            Assert.IsTrue(path.StartsWith("path.home[0]", StringComparison.Ordinal));
            Assert.IsTrue(species.StartsWith("species.source[0]", StringComparison.Ordinal));
        }

        private string GetXmlFilePath()
        {
            return Path.Combine(this.TestContext.TestDir, "..", "..", "UnitTests", "xmlHelpertest.xml");
        }

        private XmlHelper LoadXml()
        {
            XmlHelper xml = new XmlHelper();
            xml.LoadFromFile(this.GetXmlFilePath());
            return xml;
        }

        [TestMethod]
        public void Traverse()
        {
            XmlHelper xml = this.LoadXml();
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