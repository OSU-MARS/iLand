using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iLand.output
{
    /** @class OutputManager
       Global container that handles data output.
      */
    internal class OutputManager
    {
        private List<Output> mOutputs; ///< list of outputs in system

        // on creation of the output manager
        // an instance of every iLand output
        // must be added to the list of outputs.
        public OutputManager()
        {
            // add all the outputs
            mOutputs.Add(new TreeOut());
            mOutputs.Add(new TreeRemovedOut());
            mOutputs.Add(new StandOut());
            mOutputs.Add(new LandscapeOut());
            mOutputs.Add(new LandscapeRemovedOut());
            mOutputs.Add(new DynamicStandOut());
            mOutputs.Add(new ProductionOut());
            mOutputs.Add(new StandDeadOut());
            mOutputs.Add(new ManagementOut());
            mOutputs.Add(new SaplingOut());
            mOutputs.Add(new SaplingDetailsOut());
            mOutputs.Add(new CarbonOut());
            mOutputs.Add(new CarbonFlowOut());
            mOutputs.Add(new WaterOut());
        }

        public void addOutput(Output output)
        {
            mOutputs.Add(output);
        }

        public void removeOutput(string tableName)
        {
            Output o = find(tableName);
            if (o != null)
            {
                mOutputs.RemoveAt(mOutputs.IndexOf(o));
            }
        }

        public void setup()
        {
            //close();
            XmlHelper xml = GlobalSettings.instance().settings();
            string nodepath;
            foreach (Output o in mOutputs)
            {
                nodepath = String.Format("output.{0}", o.tableName());
                xml.setCurrentNode(nodepath);
                Debug.WriteLine("setup of output " + o.name());
                o.setup();
                bool enabled = xml.valueBool(".enabled", false);
                o.setEnabled(enabled);
                if (enabled)
                {
                    o.open();
                }
            }
        }

        public Output find(string tableName)
        {
            foreach (Output p in mOutputs)
            {
                if (p.tableName() == tableName)
                {
                    return p;
                }
            }
            return null;
        }

        public void save()
        {
        }

        public void close()
        {
            Debug.WriteLine("outputs closed");
            foreach (Output p in mOutputs)
            {
                p.close();
            }
        }

        public bool execute(string tableName)
        {
            using DebugTimer t = new DebugTimer("public execute()");
            t.setSilent();
            Output p = find(tableName);
            if (p != null)
            {
                if (!p.isEnabled())
                {
                    return false;
                }
                if (!p.isOpen())
                {
                    return false;
                }
                if (!p.isRowEmpty())
                {
                    Trace.TraceWarning("Output " + p.name() + " invalid (not at new row)!!!");
                    return false;
                }

                p.exec();

                return true;
            }
            Debug.WriteLine("output " + tableName + " not found!");
            return false; // no output found
        }

        public string wikiFormat()
        {
            StringBuilder result = new StringBuilder();
            foreach (Output o in mOutputs)
            {
                result.Append(o.wikiFormat() + System.Environment.NewLine);
            }
            return result.ToString();
        }
    }
}
