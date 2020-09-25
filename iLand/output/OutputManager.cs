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
        private readonly List<Output> mOutputs; ///< list of outputs in system

        // on creation of the output manager
        // an instance of every iLand output
        // must be added to the list of outputs.
        public OutputManager()
        {
            // add all the outputs
            mOutputs = new List<Output>() 
            {
                new TreeOut(),
                new TreeRemovedOut(),
                new StandOut(),
                new LandscapeOut(),
                new LandscapeRemovedOut(),
                new DynamicStandOut(),
                new ProductionOut(),
                new StandDeadOut(),
                new ManagementOut(),
                new SaplingOut(),
                new SaplingDetailsOut(),
                new CarbonOut(),
                new CarbonFlowOut(),
                new WaterOut()
            };
        }

        public void AddOutput(Output output)
        {
            mOutputs.Add(output);
        }

        public void RemoveOutput(string tableName)
        {
            Output o = Find(tableName);
            if (o != null)
            {
                mOutputs.RemoveAt(mOutputs.IndexOf(o));
            }
        }

        public void Setup()
        {
            //close();
            XmlHelper xml = GlobalSettings.Instance.Settings;
            string nodepath;
            foreach (Output o in mOutputs)
            {
                nodepath = String.Format("output.{0}", o.TableName);
                xml.SetCurrentNode(nodepath);
                Debug.WriteLine("setup of output " + o.Name);
                o.Setup();
                o.IsEnabled = xml.ValueBool(".enabled", false);
                if (o.IsEnabled)
                {
                    o.Open();
                }
            }
        }

        public Output Find(string tableName)
        {
            foreach (Output p in mOutputs)
            {
                if (p.TableName == tableName)
                {
                    return p;
                }
            }
            return null;
        }

        public void Save()
        {
        }

        public void Close()
        {
            Debug.WriteLine("outputs closed");
            foreach (Output p in mOutputs)
            {
                p.Close();
            }
        }

        public bool Execute(string tableName)
        {
            using DebugTimer t = new DebugTimer("public execute()");
            Output p = Find(tableName);
            if (p != null)
            {
                if (!p.IsEnabled)
                {
                    return false;
                }
                if (!p.IsOpen)
                {
                    return false;
                }
                if (!p.IsRowEmpty())
                {
                    Trace.TraceWarning("Output " + p.Name + " invalid (not at new row)!!!");
                    return false;
                }

                p.Exec();

                return true;
            }
            Debug.WriteLine("output " + tableName + " not found!");
            return false; // no output found
        }

        public string WikiFormat()
        {
            StringBuilder result = new StringBuilder();
            foreach (Output o in mOutputs)
            {
                result.Append(o.WikiFormat() + System.Environment.NewLine);
            }
            return result.ToString();
        }
    }
}
