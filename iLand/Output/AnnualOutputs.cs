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
    public class AnnualOutputs : IDisposable
    {
        private SqliteConnection? database;
        private readonly List<AnnualOutput> enabledOutputs;
        private int firstUncommittedYear;
        private bool isDisposed;
        private readonly int logCommitIntervalInYears;
        private SqliteTransaction? loggingTransaction;

        public LandscapeRemovedAnnualOutput? LandscapeRemoved { get; private set; }
        public TreeRemovedAnnualOutput? TreeRemoved { get; private set; }

        // on creation of the output manager
        // an instance of every iLand output
        // must be added to the list of outputs.
        public AnnualOutputs()
        {
            this.database = null; // initialized in Setup()
            this.enabledOutputs = new List<AnnualOutput>();
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
            if (model.Project.Output.Annual.Carbon.Enabled)
            {
                this.enabledOutputs.Add(new CarbonAnnualOutput());
            }
            if (model.Project.Output.Annual.CarbonFlow.Enabled)
            {
                this.enabledOutputs.Add(new CarbonFlowAnnualOutput());
            }
            if (model.Project.Output.Annual.DynamicStand.Enabled)
            {
                this.enabledOutputs.Add(new DynamicStandAnnualOutput());
            }
            if (model.Project.Output.Annual.Landscape.Enabled)
            {
                this.enabledOutputs.Add(new LandscapeTreeSpeciesAnnualOutput());
            }
            if (model.Project.Output.Annual.LandscapeRemoved.Enabled)
            {
                this.LandscapeRemoved = new LandscapeRemovedAnnualOutput();
                this.enabledOutputs.Add(this.LandscapeRemoved);
            }
            if (model.Project.Output.Annual.ProductionMonth.Enabled)
            {
                this.enabledOutputs.Add(new ProductionAnnualOutput());
            }
            if (model.Project.Output.Annual.Management.Enabled)
            {
                this.enabledOutputs.Add(new ManagementAnnualOutput());
            }
            if (model.Project.Output.Annual.SaplingDetail.Enabled)
            {
                this.enabledOutputs.Add(new SaplingDetailsAnnualOutput());
            }
            if (model.Project.Output.Annual.Sapling.Enabled)
            {
                this.enabledOutputs.Add(new SaplingAnnualOutput());
            }
            if (model.Project.Output.Annual.Stand.Enabled)
            {
                this.enabledOutputs.Add(new StandAnnualOutput());
            }
            if (model.Project.Output.Annual.StandDead.Enabled)
            {
                this.enabledOutputs.Add(new StandDeadAnnualOutput());
            }
            if (model.Project.Output.Annual.Tree.Enabled)
            {
                this.enabledOutputs.Add(new TreesAnnualOutput());
            }
            if (model.Project.Output.Annual.TreeRemoved.Enabled)
            {
                this.TreeRemoved = new TreeRemovedAnnualOutput();
                this.enabledOutputs.Add(this.TreeRemoved);
            }
            if (model.Project.Output.Annual.Water.Enabled)
            {
                this.enabledOutputs.Add(new WaterAnnualOutput());
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
            string? outputDatabaseFile = model.Project.Output.Annual.DatabaseFile;
            if (String.IsNullOrWhiteSpace(outputDatabaseFile))
            {
                throw new XmlException("The /project/output/databaseFile element is missing or does not specify an output database file name.");
            }
            string outputDatabasePath = model.Project.GetFilePath(ProjectDirectory.Output, outputDatabaseFile);
            // dbPath.Replace("$id$", maxID.ToString(), StringComparison.Ordinal);
            outputDatabasePath = outputDatabasePath.Replace("$date$", DateTime.Now.ToString("yyyyMMdd_hhmmss"), StringComparison.Ordinal);
            this.database = Landscape.GetDatabaseConnection(outputDatabasePath, openReadOnly: false);

            using SqliteTransaction outputTableCreationTransaction = this.database.BeginTransaction();
            foreach (AnnualOutput output in this.enabledOutputs)
            {
                output.Setup(model);
                output.Open(outputTableCreationTransaction);
            }
            outputTableCreationTransaction.Commit();
        }

        public void LogYear(Model model)
        {
            if (this.enabledOutputs.Count == 0)
            {
                return; // nothing to do as no outputs are enabled
            }

            if (this.loggingTransaction == null)
            {
                if (this.database == null)
                {
                    throw new NotSupportedException("Attempt to call LogYear() without first calling Setup().");
                }
                this.loggingTransaction = this.database.BeginTransaction();
                this.firstUncommittedYear = model.CurrentYear;
            }
            foreach (AnnualOutput output in this.enabledOutputs)
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
