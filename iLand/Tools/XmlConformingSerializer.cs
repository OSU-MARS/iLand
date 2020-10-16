using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace iLand.Tools
{
    internal class XmlConformingSerializer : XmlSerializer
    {
        public XmlConformingSerializer(Type type)
            : base(type)
        {
            this.UnknownAttribute += this.LoadProjectUnknownAttribute;
            this.UnknownElement += this.LoadProjectUnknownElement;
            this.UnknownNode += this.LoadProjectUnknownNode;
            this.UnreferencedObject += this.LoadProjectUnreferencedObject;
        }

        public T Deserialize<T>(Stream stream)
        {
            return (T)base.Deserialize(stream);
        }

        private void LoadProjectUnreferencedObject(object sender, UnreferencedObjectEventArgs e)
        {
            throw new XmlException(e.UnreferencedId);
        }

        private void LoadProjectUnknownNode(object sender, XmlNodeEventArgs e)
        {
            throw new XmlException(e.Name, null, e.LineNumber, e.LinePosition);
        }

        private void LoadProjectUnknownElement(object sender, XmlElementEventArgs e)
        {
            throw new XmlException(e.Element.Name, null, e.LineNumber, e.LinePosition);
        }

        private void LoadProjectUnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
            throw new XmlException(e.Attr.Name, null, e.LineNumber, e.LinePosition);
        }
    }
}
