using iLand.Simulation;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;

namespace iLand.Output
{
    /** @class OutputManager
       Global container that handles data output.
      */
    public class OutputManager
    {
        private readonly List<Output> outputs; // list of outputs in system

        public CarbonFlowOutput CarbonFlow { get; private set; }
        public CarbonOutput Carbon { get; private set; }
        public DynamicStandOutput DynamicStand { get; private set; }
        public LandscapeOutput Landscape { get; private set; }
        public LandscapeRemovedOutput LandscapeRemoved { get; private set; }
        public ManagementOutput Management { get; private set; }
        public ProductionOutput Production { get; private set; }
        public SaplingOutput Sapling { get; private set; }
        public SaplingDetailsOutput SaplingDetails { get; private set; }
        public StandDeadOutput StandDead { get; private set; }
        public StandOutput Stand { get; private set; }
        public TreeOutput Tree { get; private set; }
        public TreeRemovedOutput TreeRemoved { get; private set; }
        public WaterOutput Water { get; private set; }

        // on creation of the output manager
        // an instance of every iLand output
        // must be added to the list of outputs.
        public OutputManager()
        {
            this.CarbonFlow = new CarbonFlowOutput();
            this.Carbon = new CarbonOutput();
            this.DynamicStand = new DynamicStandOutput();
            this.Landscape = new LandscapeOutput();
            this.LandscapeRemoved = new LandscapeRemovedOutput();
            this.Production = new ProductionOutput();
            this.Management = new ManagementOutput();
            this.StandDead = new StandDeadOutput();
            this.SaplingDetails = new SaplingDetailsOutput();
            this.Sapling = new SaplingOutput();
            this.Stand = new StandOutput();
            this.Tree = new TreeOutput();
            this.TreeRemoved = new TreeRemovedOutput();
            this.Water = new WaterOutput();

            // add all the outputs
            this.outputs = new List<Output>() 
            {
                this.Tree,
                this.TreeRemoved,
                this.Stand,
                this.Landscape,
                this.LandscapeRemoved,
                this.DynamicStand,
                this.Production,
                this.StandDead,
                this.Management,
                this.Sapling,
                this.SaplingDetails,
                this.Carbon,
                this.CarbonFlow,
                this.Water
            };
        }

        public void Setup(Model model)
        {
            //Close();
            this.CarbonFlow.IsEnabled = model.Project.Output.Carbon.Enabled;
            this.Carbon.IsEnabled = model.Project.Output.Carbon.Enabled;
            this.DynamicStand.IsEnabled = model.Project.Output.DynamicStand.Enabled;
            this.Landscape.IsEnabled = model.Project.Output.Landscape.Enabled;
            this.LandscapeRemoved.IsEnabled = model.Project.Output.LandscapeRemoved.Enabled;
            this.Production.IsEnabled = model.Project.Output.ProductionMonth.Enabled;
            this.Management.IsEnabled = model.Project.Output.Management.Enabled;
            this.StandDead.IsEnabled = model.Project.Output.StandDead.Enabled;
            this.SaplingDetails.IsEnabled = model.Project.Output.SaplingDetail.Enabled;
            this.Sapling.IsEnabled = model.Project.Output.Sapling.Enabled;
            this.Stand.IsEnabled = model.Project.Output.Stand.Enabled;
            this.Tree.IsEnabled = model.Project.Output.Tree.Enabled;
            this.TreeRemoved.IsEnabled = model.Project.Output.TreeRemoved.Enabled;
            this.Water.IsEnabled = model.Project.Output.Water.Enabled;

            foreach (Output output in outputs)
            {
                if (output.IsEnabled)
                {
                    output.Setup(model);
                    output.Open(model);
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
            //using DebugTimer timer = model.DebugTimers.Create("OutputManager.LogYear()");
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
