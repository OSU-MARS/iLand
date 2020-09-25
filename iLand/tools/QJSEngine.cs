using System;
using System.Collections.Generic;

namespace iLand.tools
{
    internal class QJSEngine
    {
        private readonly Dictionary<string, QJSValue> properties;

        public QJSEngine()
        {
            this.properties = new Dictionary<string, QJSValue>();
        }

        public void CollectGarbage()
        {
            throw new NotImplementedException();
        }

        public QJSValue Evaluate(string command)
        {
            throw new NotImplementedException();
        }

        public QJSValue Evaluate(string includeFile, string path)
        {
            throw new NotImplementedException();
        }

        public string ExecuteScript(string command)
        {
            throw new NotImplementedException();
        }

        public QJSEngine GlobalObject()
        {
            return this;
        }

        public void LoadScript(string fileName)
        {
            throw new NotImplementedException();
        }

        public bool HasProperty(string name)
        {
            return this.properties.ContainsKey(name);
        }

        public QJSValue Property(string name)
        {
            return this.properties[name];
        }

        public QJSValue NewQObject(object value)
        {
            return new QJSValue(value);
        }

        public void SetProperty(string name, object value)
        {
            if (this.HasProperty(name))
            {
                this.properties[name] = new QJSValue(value);
            }
            else 
            {
                this.properties.Add(name, new QJSValue(value));
            }
        }
    }
}
