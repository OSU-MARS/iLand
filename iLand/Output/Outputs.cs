using iLand.Simulation;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace iLand.Output
{
    /** @class OutputManager
       Global container that handles data output.
      */
    public class Outputs : IDisposable
    {
        private bool isDisposed;
        private readonly List<Output> outputs; // list of outputs in system

        public SqliteConnection Database { get; private set; }

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
        public Outputs()
        {
            this.Database = null; // initialized in Setup()
            this.isDisposed = false;

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

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    if (this.Database != null)
                    {
                        this.Database.Dispose();
                    }
                }

                isDisposed = true;
            }
        }

        public void Setup(Model model)
        {
            // create run-metadata
            //int maxID = (int)(long)SqlHelper.QueryValue("select max(id) from runs", g.DatabaseInput);
            //maxID++;
            //SqlHelper.ExecuteSql(String.Format("insert into runs (id, timestamp) values ({0}, '{1}')", maxID, timestamp), g.DatabaseInput);
            // replace path information
            // setup final path
            string outputDatabaseFile = model.Project.System.Database.Out;
            if (String.IsNullOrWhiteSpace(outputDatabaseFile))
            {
                throw new XmlException("The /project/system/database/out element is missing or does not specify an output database file name.");
            }
            string outputDatabasePath = model.Files.GetPath(outputDatabaseFile, "output");
            // dbPath.Replace("$id$", maxID.ToString(), StringComparison.Ordinal);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_hhmmss");
            outputDatabasePath.Replace("$date$", timestamp, StringComparison.Ordinal);
            this.Database = model.Files.GetDatabaseConnection(outputDatabasePath, openReadOnly: false);

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
            using SqliteTransaction transaction = this.Database.BeginTransaction();
            foreach (Output output in this.outputs)
            {
                if (output.IsEnabled && output.IsOpen)
                {
                    output.LogYear(model, transaction);
                }
            }
            transaction.Commit();
        }

        //public string WikiFormat()
        //{
        //    StringBuilder result = new StringBuilder();
        //    foreach (Output output in outputs)
        //    {
        //        result.AppendLine(output.WriteHeaderToWiki());
        //    }
        //    return result.ToString();
        //}
    }
}
