using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iLand.abe
{
    internal class Events
    {
        private QJSValue mInstance; ///< object holding the events
        private Dictionary<string, QJSValue> mEvents; ///< list of event names and javascript functions

        public Events()
        {
            mEvents = new Dictionary<string, QJSValue>();
        }

        public void clear()
        {
            mEvents.Clear();
        }

        public void setup(QJSValue js_value, List<string> event_names)
        {
            mInstance = js_value; // save the object that contains the events
            foreach (string eventName in event_names)
            {
                QJSValue val = FMSTP.valueFromJs(js_value, eventName);
                if (val.isCallable())
                {
                    mEvents.Add(eventName, js_value); // save the event functions (and the name of the property that the function is assigned to)
                }
            }
        }

        public QJSValue run(string eventName, FMStand stand, List<QJSValue> parameters = null)
        {
            if (mEvents.ContainsKey(eventName))
            {
                if (stand != null)
                {
                    FomeScript.setExecutionContext(stand);
                }
                QJSValue func = mEvents[eventName].property(eventName);
                QJSValue result = null;
                if (func.isCallable())
                {
                    using DebugTimer t = new DebugTimer("ABE:JSEvents:run");

                    if (parameters != null)
                    {
                        result = func.callWithParameters(mInstance, parameters);
                    }
                    else
                    {
                        result = func.callWithInstance(mInstance);
                    }
                    if (FMSTP.verbose() || (stand != null && stand.trace()))
                    {
                        Debug.WriteLine((stand != null ? stand.context() : "<no stand>") + "  invoking javascript event " + eventName + " result: " + result.toString());
                    }
                }

                //Debug.WriteLine("event called:" + eventName + "result:" + result.toString();
                if (result.isError())
                {
                    throw new NotSupportedException(String.Format("{2} Javascript error in event {0}: {1}", eventName, result.toString(), stand != null ? stand.context() : "----"));
                }
                return result;
            }
            return new QJSValue();
        }

        public bool hasEvent(string eventName)
        {
            return mEvents.ContainsKey(eventName);
        }

        public string dump()
        {
            StringBuilder event_list = new StringBuilder("Registered events: ");
            foreach (string eventName in mEvents.Keys)
            {
                event_list.Append(eventName + " ");
            }
            return event_list.ToString();
        }
    }
}
