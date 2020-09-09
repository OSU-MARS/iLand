using iLand.tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace iLand.Test
{
    [TestClass]
    public class TestXmlHelper
    {
        private XmlHelper xml;

        [ClassInitialize]
        public void initTestCase()
        {
            xml.loadFromFile("E:\\dev\\iland\\src\\tests\\testXmlHelper\\xmlHelperTest.xml");
        }

        [TestMethod]
        public void dump()
        {
            string test = xml.dump("test");
            string path = xml.dump("path");
            string species = xml.dump("species");
        }

        [TestMethod]
        public void filepath()
        {
            // setup file paths...
            xml.dump("path");
            GlobalSettings.instance().setupDirectories(xml.node("path"), null);
        }

        [TestMethod]
        public void traverse()
        {
            Assert.IsTrue(xml.node("") == null); // top node
            Assert.IsTrue(xml.node("test.block.a") == null); // traverse
            xml.setCurrentNode("test.block");
            Assert.IsTrue(xml.node(".b") == null);
            Assert.IsTrue(xml.node(".b.c") == null);
            Assert.IsTrue(xml.node("test.block.b.c") == null);
            Assert.IsTrue(xml.node(".b[0]") == null);
            Assert.IsTrue(xml.node(".b[0].d") == null);
            Assert.IsTrue(xml.node("test.block[1].n[2].o") == null);

            Assert.IsTrue(xml.value("test.block.b.c") == "c");
            Assert.IsTrue(xml.value("test.block.b.c.nonexistent", "0") == "0");
            Assert.IsTrue(xml.value("test.block[1].n[2].o") == "o");
        }
    }
}