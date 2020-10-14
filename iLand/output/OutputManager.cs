using iLand.Core;
using iLand.Tools;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace iLand.Output
{
    /** @class OutputManager
       Global container that handles data output.
      */
    public class OutputManager
    {
        private readonly List<Output> outputs; ///< list of outputs in system

        public CarbonFlowOutput CarbonFlowOutput { get; private set; }
        public CarbonOutput CarbonOutput { get; private set; }
        public DynamicStandOutput DynamicStandOutput { get; private set; }
        public LandscapeOutput LandscapeOutput { get; private set; }
        public LandscapeRemovedOutput LandscapeRemovedOutput { get; private set; }
        public ManagementOutput ManagementOutput { get; private set; }
        public ProductionOutput ProductionOutput { get; private set; }
        public SaplingOutput SaplingOutput { get; private set; }
        public SaplingDetailsOutput SaplingDetailsOutput { get; private set; }
        public StandDeadOutput StandDeadOutput { get; private set; }
        public StandOutput StandOutput { get; private set; }
        public TreeOutput TreeOutput { get; private set; }
        public TreeRemovedOutput TreeRemovedOutput { get; private set; }
        public WaterOutput WaterOutput { get; private set; }

        // on creation of the output manager
        // an instance of every iLand output
        // must be added to the list of outputs.
        public OutputManager()
        {
            this.CarbonFlowOutput = new CarbonFlowOutput();
            this.CarbonOutput = new CarbonOutput();
            this.DynamicStandOutput = new DynamicStandOutput();
            this.LandscapeOutput = new LandscapeOutput();
            this.LandscapeRemovedOutput = new LandscapeRemovedOutput();
            this.ProductionOutput = new ProductionOutput();
            this.ManagementOutput = new ManagementOutput();
            this.StandDeadOutput = new StandDeadOutput();
            this.SaplingDetailsOutput = new SaplingDetailsOutput();
            this.SaplingOutput = new SaplingOutput();
            this.StandOutput = new StandOutput();
            this.TreeOutput = new TreeOutput();
            this.TreeRemovedOutput = new TreeRemovedOutput();
            this.WaterOutput = new WaterOutput();

            // add all the outputs
            this.outputs = new List<Output>() 
            {
                this.TreeOutput,
                this.TreeRemovedOutput,
                this.StandOutput,
                this.LandscapeOutput,
                this.LandscapeRemovedOutput,
                this.DynamicStandOutput,
                this.ProductionOutput,
                this.StandDeadOutput,
                this.ManagementOutput,
                this.SaplingOutput,
                this.SaplingDetailsOutput,
                this.CarbonOutput,
                this.CarbonFlowOutput,
                this.WaterOutput
            };
        }

        public void Setup(GlobalSettings globalSettings)
        {
            //close();
            XmlHelper xml = globalSettings.Settings;
            foreach (Output output in outputs)
            {
                string nodepath = String.Format("output.{0}", output.TableName);
                xml.TrySetCurrentNode(nodepath);
                output.Setup(globalSettings);
                output.IsEnabled = xml.GetBool(".enabled", false);
                if (output.IsEnabled)
                {
                    output.Open(globalSettings);
                }
            }
        }

        //public Output Find(string tableName)
        //{
        //    foreach (Output output in mOutputs)
        //    {
        //        if (output.TableName == tableName)
        //        {
        //            return output;
        //        }
        //    }
        //    return null;
        //}

        public void LogYear(Model model)
        {
            using DebugTimer timer = model.DebugTimers.Create("OutputManager.LogYear()");
            using SqliteTransaction transaction = model.GlobalSettings.DatabaseOutput.BeginTransaction();
            foreach (Output output in this.outputs)
            {
                if (output.IsEnabled && output.IsOpen)
                {
                    output.LogYear(model, transaction);
                }
            }
            transaction.Commit();
        }

        public string WikiFormat()
        {
            StringBuilder result = new StringBuilder();
            foreach (Output output in outputs)
            {
                result.AppendLine(output.WriteHeaderToWiki());
            }
            return result.ToString();
        }
    }
}
