using iLand.Input.ProjectFile;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Xml;
using Model = iLand.Simulation.Model;

namespace iLand.Output
{
    /** @class OutputManager
       Global container that handles data output.
      */
    public class Outputs : IDisposable
    {
        private SqliteConnection? database;
        private readonly List<Output> enabledOutputs;
        private int firstUncommittedYear;
        private bool isDisposed;
        private readonly int logCommitIntervalInYears;
        private SqliteTransaction? loggingTransaction;

        public LandscapeRemovedOutput? LandscapeRemoved { get; private set; }
        public TreeRemovedOutput? TreeRemoved { get; private set; }

        // on creation of the output manager
        // an instance of every iLand output
        // must be added to the list of outputs.
        public Outputs()
        {
            this.database = null; // initialized in Setup()
            this.enabledOutputs = new List<Output>();
            this.firstUncommittedYear = -1;
            this.isDisposed = false;
            this.logCommitIntervalInYears = 10; // 
            this.loggingTransaction = null; // managed in LogYear()

            this.LandscapeRemoved = null;
            this.TreeRemoved = null;
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
                    if (this.loggingTransaction != null)
                    {
                        this.loggingTransaction.Commit();
                        this.loggingTransaction.Dispose();
                    }
                    if (this.database != null)
                    {
                        this.database.Dispose();
                    }
                }

                isDisposed = true;
            }
        }

        public void Setup(Model model)
        {
            if (model.Project.Output.Carbon.Enabled)
            {
                this.enabledOutputs.Add(new CarbonOutput());
            }
            if (model.Project.Output.CarbonFlow.Enabled)
            {
                this.enabledOutputs.Add(new CarbonFlowOutput());
            }
            if (model.Project.Output.DynamicStand.Enabled)
            {
                this.enabledOutputs.Add(new DynamicStandOutput());
            }
            if (model.Project.Output.Landscape.Enabled)
            {
                this.enabledOutputs.Add(new LandscapeOutput());
            }
            if (model.Project.Output.LandscapeRemoved.Enabled)
            {
                this.LandscapeRemoved = new LandscapeRemovedOutput();
                this.enabledOutputs.Add(this.LandscapeRemoved);
            }
            if (model.Project.Output.ProductionMonth.Enabled)
            {
                this.enabledOutputs.Add(new ProductionOutput());
            }
            if (model.Project.Output.Management.Enabled)
            {
                this.enabledOutputs.Add(new ManagementOutput());
            }
            if (model.Project.Output.SaplingDetail.Enabled)
            {
                this.enabledOutputs.Add(new SaplingDetailsOutput());
            }
            if (model.Project.Output.Sapling.Enabled)
            {
                this.enabledOutputs.Add(new SaplingOutput());
            }
            if (model.Project.Output.Stand.Enabled)
            {
                this.enabledOutputs.Add(new StandOutput());
            }
            if (model.Project.Output.StandDead.Enabled)
            {
                this.enabledOutputs.Add(new StandDeadOutput());
            }
            if (model.Project.Output.Tree.Enabled)
            {
                this.enabledOutputs.Add(new TreeOutput());
            }
            if (model.Project.Output.TreeRemoved.Enabled)
            {
                this.TreeRemoved = new TreeRemovedOutput();
                this.enabledOutputs.Add(this.TreeRemoved);
            }
            if (model.Project.Output.Water.Enabled)
            {
                this.enabledOutputs.Add(new WaterOutput());
            }
            
            if (this.enabledOutputs.Count == 0)
            {
                return; // nothing to output so no reason to open output database
            }

            // create run-metadata
            //int maxID = (int)(long)SqlHelper.QueryValue("select max(id) from runs", g.DatabaseInput);
            //maxID++;
            //SqlHelper.ExecuteSql(String.Format("insert into runs (id, timestamp) values ({0}, '{1}')", maxID, timestamp), g.DatabaseInput);
            // replace path information
            // setup final path
            string? outputDatabaseFile = model.Project.Output.DatabaseFile;
            if (String.IsNullOrWhiteSpace(outputDatabaseFile))
            {
                throw new XmlException("The /project/output/databaseFile element is missing or does not specify an output database file name.");
            }
            string outputDatabasePath = model.Project.GetFilePath(ProjectDirectory.Output, outputDatabaseFile);
            // dbPath.Replace("$id$", maxID.ToString(), StringComparison.Ordinal);
            outputDatabasePath = outputDatabasePath.Replace("$date$", DateTime.Now.ToString("yyyyMMdd_hhmmss"), StringComparison.Ordinal);
            this.database = Landscape.GetDatabaseConnection(outputDatabasePath, openReadOnly: false);

            using SqliteTransaction outputTableCreationTransaction = this.database.BeginTransaction();
            foreach (Output output in this.enabledOutputs)
            {
                output.Setup(model);
                output.Open(outputTableCreationTransaction);
            }
            outputTableCreationTransaction.Commit();
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
            if (this.enabledOutputs.Count == 0)
            {
                return; // nothing to do as no outputs are enabled
            }

            //using DebugTimer timer = model.DebugTimers.Create("OutputManager.LogYear()");
            if (this.loggingTransaction == null)
            {
                if (this.database == null)
                {
                    throw new NotSupportedException("Attempt to call LogYear() without first calling Setup().");
                }
                this.loggingTransaction = this.database.BeginTransaction();
                this.firstUncommittedYear = model.CurrentYear;
            }
            foreach (Output output in this.enabledOutputs)
            {
                output.LogYear(model, this.loggingTransaction);
            }
            if (model.CurrentYear - this.firstUncommittedYear > this.logCommitIntervalInYears)
            {
                this.loggingTransaction.Commit();
                this.loggingTransaction.Dispose();
                this.loggingTransaction = null;
            }
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
