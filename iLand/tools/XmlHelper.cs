using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace iLand.tools
{
    /** @class XmlHelper
      XmlHelper wraps a XML file and provides some convenient functions to
      retrieve values. Internally XmlHelper uses a QDomDocument (the full structure is
      kept in memory so the size is restricted).
      Use node() to get a XmlNode or use value() to directly retrieve the node value.
      Nodes could be addressed relative to a node defined by setCurrentNode() using a ".".
      The notation is as follows:
      - a '.' character defines a hierarchy
      - [] the Nth element of the same hierarchical layer can be retrieved by [n-1]
      Use also the convenience functions valueBool() and valueDouble().
      While all the value/node etc. functions parse the DOM tree at every call, the data accessed by paramValue() - type
      functions is parsed only once during startup and stored in a QVariant array. Accessible are all nodes that are children of the
       "<parameter>"-node.

      @code
      XmlNode e,f
      e = xml.node("first.second.third"); // e points to "third"
      xml.setCurrentNode("first");
      f = xml.node(".second.third"); // e and f are equal
      e = xml.node("level1[2].level2[3].level3"); // 3rd element of level 1, ...
      int x = xml.value(".second", "0").toInt(); // node value of "second" or "0" if not found.
      if (xml.valueBool("enabled")) // use of valueBool with default value (false)
         ...
      XmlHelper xml_sec(xml.node("first.second")); // create a xml-helper with top node=first.second
      xml_sec.valueDouble("third"); // use this
      @endcode

      */
    internal class XmlHelper
    {
        private XmlDocument mDoc;
        private XmlNode mCurrentTop;
        private XmlNode mTopNode;
        private Dictionary<string, string> mParamCache;

        // relative top nodes
        public XmlNode top() { return mTopNode; }
        public void setCurrentNode(string path) { mCurrentTop = node(path); } ///< sets @p path as the current (relative) node.
        public void setCurrentNode(XmlNode node) { mCurrentTop = node; } ///< sets node as the current (relative) top node.
                                                                          /// returns true if the current (relative!) node is valid (i.e. not null).
        public bool isValid() { return mCurrentTop != null; }

        public XmlHelper()
        {
        }

        public XmlHelper(string fileName)
        {
            loadFromFile(fileName);
        }

        /** Create a XmlHelper instance with @p topNode as top node.
            The xml tree is not copied.
            */
        public XmlHelper(XmlNode topNode)
        {
            mTopNode = topNode;
            mCurrentTop = topNode;
        }

        public void loadFromFile(string fileName)
        {
            string xmlFile = Helper.loadTextFile(fileName);

            if (String.IsNullOrEmpty(xmlFile) == false)
            {
                mDoc.LoadXml(xmlFile);
            }
            else
            {
                throw new FileNotFoundException("xmlfile does not exist or is empty!", fileName);
            }
            mCurrentTop = mDoc.DocumentElement; // top element
            mTopNode = mCurrentTop;

            // fill parameter cache
            XmlNode e = node("model.parameter");
            e = e.FirstChild;
            mParamCache.Clear();
            while (e != null)
            {
                mParamCache[e.Name] = e.InnerText;
                e = e.NextSibling;
            }
        }

        /** numeric values of elements in the section <parameter> are stored in a QHash structure for faster access.
            with paramValue() these data can be accessed.
          */
        public double paramValue(string paramName, double defaultValue)
        {
            if (mParamCache.ContainsKey(paramName))
            {
                return Double.Parse(mParamCache[paramName]);
            }
            return defaultValue;
        }

        public string paramValueString(string paramName, string defaultValue = "")
        {
            if (mParamCache.ContainsKey(paramName))
            {
                return mParamCache[paramName];
            }
            return defaultValue;
        }

        public bool paramValueBool(string paramName, bool defaultValue = true)
        {
            if (mParamCache.ContainsKey(paramName))
            {
                string v = mParamCache[paramName].Trim();
                bool ret = (v == "1" || v == "true");
                return ret;
            }
            return defaultValue;
        }

        public bool hasNode(string path)
        {
            return node(path) != null;
        }

        public string value(string path, string defaultValue = "")
        {
            XmlNode e = node(path);
            if (e == null)
            {
                Debug.WriteLine("Warning: xml: node " + path + " is not present.");
                return defaultValue;
            }
            else
            {
                if (String.IsNullOrEmpty(e.InnerText))
                {
                    return defaultValue;
                }
                else
                {
                    return e.InnerText;
                }
            }
        }

        public bool valueBool(string path, bool defaultValue = false)
        {
            XmlNode e = node(path);
            if (e == null)
            {
                Debug.WriteLine("Warning: xml: node " + path + " is not present.");
                return defaultValue;
            }
            string v = e.InnerText;
            if (v == "true" || v == "True" || v == "1")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public double valueDouble(string path, double defaultValue = 0)
        {
            XmlNode e = node(path);
            if (e == null)
            {
                Debug.WriteLine("Warning: xml: node " + path + " is not present.");
                return defaultValue;
            }
            else
            {
                if (String.IsNullOrEmpty(e.InnerText))
                {
                    return defaultValue;
                }
                else
                {
                    return Double.Parse(e.InnerText);
                }
            }
        }

        public int valueInt(string path, int defaultValue)
        {
            return (int)valueDouble(path, defaultValue);
        }

        /// retrives node with given @p path and a element where isNull() is true if nothing is found.
        public XmlNode node(string path)
        {
            string[] elem = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            XmlNode c;
            if (path.Length > 0 && path[0] == '.')
            {
                c = mCurrentTop;
            }
            else
            {
                c = mTopNode;
            }
            foreach (string level in elem)
            {
                if (level.IndexOf('[') < 0)
                {
                    c = c.SelectSingleNode(level);
                    if (c == null)
                    {
                        break;
                    }
                }
                else
                {
                    int pos = level.IndexOf('[');
                    string levelWithoutClosingBracket = level.Substring(0, level.Length - 1); // drop closing bracket
                    int ind = Int32.Parse(level.Substring(levelWithoutClosingBracket.Length - pos - 1));
                    string name = levelWithoutClosingBracket.Substring(0, pos);
                    c = c.SelectSingleNode(name);
                    while (ind > 0 && c != null)
                    {
                        c = c.NextSibling;
                        ind--;
                    }
                    if (c == null)
                    {
                        break;
                    }
                }
            }
            //Debug.WriteLine("node-request:" << path;
            return c;
        }

        // writers
        public bool setNodeValue(XmlNode node, string value)
        {
            if (node != null && node.FirstChild != null) // BUGBUG: silent no op
            {
                node.FirstChild.InnerText = value;
                return true;
            }
            return false;
        }

        public bool setNodeValue(string path, string value)
        {
            XmlNode e = node(path);
            if (e == null)
            {
                Debug.WriteLine("XML: attempting to set value of " + path + ": node not present.");
                return false;
            }
            return setNodeValue(e, value);
        }

        // private recursive loop
        private void dump_rec(XmlNode c, List<string> stack, List<string> dump)
        {
            if (c == null)
            {
                return;
            }
            XmlNode ch = c.FirstChild;
            bool hasChildren = ch != null;
            bool nChildren = hasChildren && ch.NextSibling != null;
            int child_index = -1;
            while (ch != null)
            {
                if (nChildren)
                {
                    child_index++;
                    stack.Add(String.Format("{0}[{1}]", ch.Name, child_index));
                }
                else
                {
                    stack.Add(ch.Name);
                }
                dump_rec(ch, stack, dump);
                stack.RemoveAt(stack.Count - 1);
                ch = ch.NextSibling;
            }

            string self = String.Empty;
            if (!hasChildren)
            {
                self = c.InnerText;
            }
            self = String.Format("{0}: {1}", String.Join(".", stack), self);
            dump.Add(self);
        }

        public string dump(string path, int levels = -1)
        {
            XmlNode c = node(path);
            List<string> stack = new List<string>() { c.Name };
            List<string> result = new List<string>();
            dump_rec(c, stack, result);
            return String.Join(Environment.NewLine, result);
        }
    }
}
