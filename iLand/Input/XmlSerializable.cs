using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace iLand.Input
{
    public abstract class XmlSerializable : IXmlSerializable
    {
        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            XmlReader elementReader = reader.ReadSubtree();
            elementReader.MoveToContent();
            while (elementReader.EOF == false)
            {
                if (elementReader.IsStartElement())
                {
                    this.ReadStartElement(elementReader);
                }
                else
                {
                    elementReader.Read();
                }
            }
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            throw new NotSupportedException();
        }

        protected abstract void ReadStartElement(XmlReader reader);
    }
}
