using System;
using System.Collections.Generic;

namespace iLand.tools
{
    internal class QJSValue
    {
        private readonly object value;

        public QJSValue()
            : this(null)
        {
        }

        public QJSValue(object obj)
        {
            this.value = obj;
        }

        public QJSValue Call()
        {
            throw new NotImplementedException();
        }

        public QJSValue Call(IEnumerable<QJSValue> parameters)
        {
            throw new NotImplementedException();
        }

        public QJSValue CallWithInstance(QJSValue value)
        {
            throw new NotImplementedException();
        }

        public QJSValue CallWithParameters(QJSValue value, IEnumerable<QJSValue> parameters)
        {
            throw new NotImplementedException();
        }

        public bool HasOwnProperty(string name)
        {
            throw new NotImplementedException();
        }

        public bool HasProperty(string name)
        {
            throw new NotImplementedException();
        }

        public bool IsArray()
        {
            return false;
        }

        public bool IsBool()
        {
            return false;
        }

        public bool IsCallable()
        {
            return false;
        }

        public bool IsError()
        {
            return false;
        }

        public bool IsNumber()
        {
            return false;
        }

        public bool IsObject()
        {
            return false;
        }

        public bool IsString()
        {
            return false;
        }

        public bool IsUndefined()
        {
            return false;
        }

        public QJSValue Property(string name)
        {
            throw new NotImplementedException();
        }

        public void SetProperty(string name, object value)
        {
            throw new NotImplementedException();
        }

        public bool ToBool()
        {
            throw new NotImplementedException();
        }

        public int ToInt()
        {
            throw new NotImplementedException();
        }

        public object ToQObject()
        {
            throw new NotImplementedException();
        }

        public double ToNumber()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }

        public object ToVariant()
        {
            throw new NotImplementedException();
        }
    }
}
