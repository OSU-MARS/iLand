using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace iLand.Tools
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
    public class XmlHelper
    {
        private readonly XmlDocument mDoc;
        private readonly Dictionary<string, string> mParamCache;

        public XmlNode CurrentNode { get; set; } ///< sets node as the current (relative) top node.
        public XmlNode TopNode { get; private set; }

        public XmlHelper()
        {
            this.CurrentNode = null;
            this.mDoc = new XmlDocument();
            this.mParamCache = new Dictionary<string, string>();
            this.TopNode = null;
        }

        public XmlHelper(string fileName)
            : this()
        {
            this.LoadFromFile(fileName);
        }

        /** Create a XmlHelper instance with @p topNode as top node.
            The xml tree is not copied.
            */
        public XmlHelper(XmlNode topNode)
        {
            this.TopNode = topNode ?? throw new ArgumentNullException(nameof(topNode));
            this.CurrentNode = topNode;
        }

        /// returns true if the current (relative!) node is valid (i.e. not null).
        public bool IsValid() { return CurrentNode != null; }
        ///< sets @p path as the current (relative) node.
        public bool TrySetCurrentNode(string path)
        {
            CurrentNode = Node(path);
            return this.IsValid();
        }

        public void LoadFromFile(string fileName)
        {
            string xmlFile = File.ReadAllText(fileName);
            if (String.IsNullOrEmpty(xmlFile) == false)
            {
                mDoc.LoadXml(xmlFile);
            }
            else
            {
                throw new FileNotFoundException("XML file does not exist or is empty!", fileName);
            }
            CurrentNode = mDoc.DocumentElement; // top element
            TopNode = CurrentNode;

            // fill parameter cache
            XmlNode e = Node("model.parameter");
            if (e == null)
            {
                // TODO: move to Model parsing code
                throw new XmlException("/project/model/parameter element not found in project file '" + fileName + "'.");
            }
            
            mParamCache.Clear();
            for (e = e.FirstChild; e != null; e = e.NextSibling)
            {
                mParamCache[e.Name] = e.InnerText;
            }
        }

        /** numeric values of elements in the section <parameter> are stored in a QHash structure for faster access.
            with paramValue() these data can be accessed.
          */
        public double ParamValue(string paramName, double defaultValue)
        {
            if (mParamCache.ContainsKey(paramName))
            {
                return Double.Parse(mParamCache[paramName]);
            }
            return defaultValue;
        }

        public string GetStringParameter(string paramName, string defaultValue = "")
        {
            if (mParamCache.ContainsKey(paramName))
            {
                return mParamCache[paramName];
            }
            return defaultValue;
        }

        public bool GetBooleanParameter(string paramName, bool defaultValue = true)
        {
            if (mParamCache.ContainsKey(paramName))
            {
                string valueAsString = mParamCache[paramName].Trim();
                if (Boolean.TryParse(valueAsString, out bool value) == false)
                {
                    value = String.Equals(valueAsString, "1", StringComparison.Ordinal);
                }
                return value;
            }
            return defaultValue;
        }

        public bool HasNode(string path)
        {
            return this.Node(path) != null;
        }

        public string GetString(string path, string defaultValue = "")
        {
            XmlNode e = this.Node(path);
            if (e == null)
            {
                Trace.TraceWarning("Xml: node '" + path + "' not found.");
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

        public bool GetBool(string path, bool defaultValue = false)
        {
            XmlNode e = Node(path);
            if (e == null)
            {
                Trace.TraceWarning("Xml: node '" + path + "' not found.");
                return defaultValue;
            }
            return Boolean.Parse(e.InnerText);
        }

        public double GetDouble(string path, double defaultValue = 0)
        {
            XmlNode e = Node(path);
            if (e == null)
            {
                Trace.TraceWarning("Xml: node '" + path + "' not found.");
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

        public int ValueInt(string path, int defaultValue)
        {
            return (int)GetDouble(path, defaultValue);
        }

        /// retrives node with given @p path and a element where isNull() is true if nothing is found.
        public XmlNode Node(string path)
        {
            string[] elem = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            XmlNode c;
            if (path.Length > 0 && path[0] == '.')
            {
                c = CurrentNode;
            }
            else
            {
                c = TopNode;
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
                    string levelWithoutClosingBracket = level[0..^1]; // drop closing bracket
                    int ind = Int32.Parse(level[(pos + 1)..^1]);
                    string name = level[0..pos];
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
        public bool SetNodeValue(XmlNode node, string value)
        {
            if (node != null && node.FirstChild != null) // BUGBUG: silent no op
            {
                node.FirstChild.InnerText = value;
                return true;
            }
            return false;
        }

        public bool SetNodeValue(string path, string value)
        {
            XmlNode e = Node(path);
            if (e == null)
            {
                Trace.TraceWarning("Xml: value of node '" + path + "' could not be set because it was not found.");
                return false;
            }
            return SetNodeValue(e, value);
        }

        // private recursive loop
        private void DumpRecursive(XmlNode c, List<string> stack, List<string> dump)
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
                DumpRecursive(ch, stack, dump);
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

        public string Dump(string path)
        {
            XmlNode c = Node(path);
            List<string> stack = new List<string>() { c.Name };
            List<string> result = new List<string>();
            DumpRecursive(c, stack, result);
            return String.Join(Environment.NewLine, result);
        }
    }
}
