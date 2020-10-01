using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iLand.Output
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
                new TreeOutput(),
                new TreeRemovedOutput(),
                new StandOutput(),
                new LandscapeOutput(),
                new LandscapeRemovedOutput(),
                new DynamicStandOutput(),
                new ProductionOutput(),
                new StandDeadOutput(),
                new ManagementOutput(),
                new SaplingOutput(),
                new SaplingDetailsOutput(),
                new CarbonOutput(),
                new CarbonFlowOutput(),
                new WaterOutput()
            };
        }

        public void Setup()
        {
            //close();
            XmlHelper xml = GlobalSettings.Instance.Settings;
            string nodepath;
            foreach (Output o in mOutputs)
            {
                nodepath = String.Format("output.{0}", o.TableName);
                xml.TrySetCurrentNode(nodepath);
                o.Setup();
                o.IsEnabled = xml.GetBool(".enabled", false);
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

        public void LogYear()
        {
            using DebugTimer timer = new DebugTimer("OutputManager.LogYear()");

            foreach (Output output in this.mOutputs)
            {
                if (output.IsEnabled && output.IsOpen)
                {
                    if (output.IsRowEmpty() == false)
                    {
                        Trace.TraceWarning("Output " + output.Name + " invalid (not at new row)!!!");
                        continue;
                    }
                    output.LogYear();
                }
            }
        }

        public string WikiFormat()
        {
            StringBuilder result = new StringBuilder();
            foreach (Output o in mOutputs)
            {
                result.Append(o.WriteHeaderToWiki() + System.Environment.NewLine);
            }
            return result.ToString();
        }
    }
}
