using iLand.Input.ProjectFile;
using iLand.Output.Sql;
using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Xml;
using Model = iLand.Simulation.Model;

namespace iLand.Output
{
    // Global container that handles data output.
    public class Outputs : IDisposable
    {
        private readonly SortedList<int, StandTreeStatistics> currentYearStandStatistics;
        private int mostRecentSimulationYearCommittedToDatabase;
        private bool isDisposed;
        private readonly int sqlCommitIntervalInYears;
        private readonly SqliteConnection? sqlDatabaseConnection;
        private readonly List<AnnualOutput> sqlOutputs;
        private SqliteTransaction? sqlOutputTransaction;

        public LandscapeRemovedAnnualOutput? LandscapeRemoved { get; private init; }
        public List<ResourceUnitTrajectory> ResourceUnitTrajectories { get; private init; } // in same order as Landscape.ResourceUnits
        public SortedList<int, StandTrajectory> StandTrajectoriesByID { get; private init; }
        public TreeRemovedAnnualOutput? TreeRemoved { get; private init; }

        public Outputs(Project projectFile, Landscape landscape, Model model) // TODO: remove model
        {
            this.currentYearStandStatistics = new();
            this.mostRecentSimulationYearCommittedToDatabase = -1;
            this.isDisposed = false;
            this.sqlCommitIntervalInYears = 10; // 
            this.sqlDatabaseConnection = null; // initialized in Setup()
            this.sqlOutputs = new();
            this.sqlOutputTransaction = null; // managed in LogYear()

            this.LandscapeRemoved = null;
            this.ResourceUnitTrajectories = new();
            this.StandTrajectoriesByID = new();
            this.TreeRemoved = null;

            bool logResourceUnitTrajectories = projectFile.Output.Memory.ResourceUnitTrajectories.Enabled;
            bool logStandTrajectories = projectFile.Output.Memory.StandTrajectories.Enabled;
            if (logResourceUnitTrajectories || logStandTrajectories)
            {
                List<ResourceUnit> resourceUnits = landscape.ResourceUnits;
                if (logResourceUnitTrajectories)
                {
                    this.ResourceUnitTrajectories.Capacity = resourceUnits.Count;
                }

                int previousStandID = Constant.NoDataInt32;
                for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnits.Count; ++resourceUnitIndex)
                {
                    ResourceUnit resourceUnit = resourceUnits[resourceUnitIndex];
                    if (logResourceUnitTrajectories)
                    {
                        this.ResourceUnitTrajectories.Add(new ResourceUnitTrajectory(resourceUnit));
                    }
                    if (logStandTrajectories)
                    {
                        SortedList<int, ResourceUnitTreeStatistics> standsInResourceUnit = resourceUnit.Trees.TreeStatisticsByStandID;
                        for (int standIndex = 0; standIndex < standsInResourceUnit.Count; ++standIndex)
                        {
                            int standID = standsInResourceUnit.Keys[standIndex];
                            if ((previousStandID != standID) && (this.currentYearStandStatistics.ContainsKey(standID) == false))
                            {
                                this.currentYearStandStatistics.Add(standID, new StandTreeStatistics(standID));
                                this.StandTrajectoriesByID.Add(standID, new StandTrajectory(standID));
                            }

                            previousStandID = standID;
                        }
                    }
                }
            }

            SqlOutputs sqlOutputSettings = projectFile.Output.Sql;
            if (sqlOutputSettings.Carbon.Enabled)
            {
                this.sqlOutputs.Add(new CarbonAnnualOutput());
            }
            if (sqlOutputSettings.CarbonFlow.Enabled)
            {
                this.sqlOutputs.Add(new CarbonFlowAnnualOutput());
            }
            if (sqlOutputSettings.DynamicStand.Enabled)
            {
                this.sqlOutputs.Add(new Sql.DynamicStandAnnualOutput());
            }
            if (sqlOutputSettings.Landscape.Enabled)
            {
                this.sqlOutputs.Add(new LandscapeTreeSpeciesAnnualOutput());
            }
            if (sqlOutputSettings.LandscapeRemoved.Enabled)
            {
                this.LandscapeRemoved = new LandscapeRemovedAnnualOutput();
                this.sqlOutputs.Add(this.LandscapeRemoved);
            }
            if (sqlOutputSettings.ProductionMonth.Enabled)
            {
                this.sqlOutputs.Add(new ProductionAnnualOutput());
            }
            if (sqlOutputSettings.Management.Enabled)
            {
                this.sqlOutputs.Add(new ManagementAnnualOutput());
            }
            if (sqlOutputSettings.SaplingDetail.Enabled)
            {
                this.sqlOutputs.Add(new SaplingDetailsAnnualOutput());
            }
            if (sqlOutputSettings.Sapling.Enabled)
            {
                this.sqlOutputs.Add(new SaplingAnnualOutput());
            }
            if (sqlOutputSettings.Stand.Enabled)
            {
                this.sqlOutputs.Add(new ResourceUnitLiveTreeAnnualOutput());
            }
            if (sqlOutputSettings.StandDead.Enabled)
            {
                this.sqlOutputs.Add(new ResourceUnitSnagAnnualOutput());
            }
            if (sqlOutputSettings.Tree.Enabled)
            {
                this.sqlOutputs.Add(new TreesAnnualOutput());
            }
            if (sqlOutputSettings.TreeRemoved.Enabled)
            {
                this.TreeRemoved = new TreeRemovedAnnualOutput();
                this.sqlOutputs.Add(this.TreeRemoved);
            }
            if (sqlOutputSettings.Water.Enabled)
            {
                this.sqlOutputs.Add(new WaterAnnualOutput());
            }

            if (this.sqlOutputs.Count == 0)
            {
                return; // nothing to output so no reason to open output database
            }

            // create run-metadata
            //int maxID = (int)(long)SqlHelper.QueryValue("select max(id) from runs", g.DatabaseInput);
            //maxID++;
            //SqlHelper.ExecuteSql(String.Format("insert into runs (id, timestamp) values ({0}, '{1}')", maxID, timestamp), g.DatabaseInput);
            // replace path information
            // setup final path
            string? outputDatabaseFile = sqlOutputSettings.DatabaseFile;
            if (String.IsNullOrWhiteSpace(outputDatabaseFile))
            {
                throw new XmlException("The /project/output/annual/databaseFile element is missing or does not specify an output database file name.");
            }
            string outputDatabasePath = projectFile.GetFilePath(ProjectDirectory.Output, outputDatabaseFile);
            // dbPath.Replace("$id$", maxID.ToString(), StringComparison.Ordinal);
            outputDatabasePath = outputDatabasePath.Replace("$date$", DateTime.Now.ToString("yyyyMMdd_hhmmss"), StringComparison.Ordinal);
            this.sqlDatabaseConnection = Landscape.GetDatabaseConnection(outputDatabasePath, openReadOnly: false);

            using SqliteTransaction outputTableCreationTransaction = this.sqlDatabaseConnection.BeginTransaction();
            SimulationState simulationState = model.SimulationState;
            foreach (AnnualOutput output in this.sqlOutputs)
            {
                output.Setup(projectFile, simulationState);
                output.Open(outputTableCreationTransaction);
            }
            outputTableCreationTransaction.Commit();
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
                    if (this.sqlOutputTransaction != null)
                    {
                        this.sqlOutputTransaction.Commit();
                        this.sqlOutputTransaction.Dispose();
                    }
                    if (this.sqlDatabaseConnection != null)
                    {
                        this.sqlDatabaseConnection.Dispose();
                    }
                }

                isDisposed = true;
            }
        }

        public void LogYear(Model model)
        {
            // in memory
            bool logResourceUnitTrajectories = this.ResourceUnitTrajectories.Count > 0;
            bool logStandTrajectories = this.StandTrajectoriesByID.Count > 0;
            if (logResourceUnitTrajectories || logStandTrajectories)
            {
                for (int resourceUnitIndex = 0; resourceUnitIndex < this.ResourceUnitTrajectories.Count; ++ resourceUnitIndex)
                {
                    if (logResourceUnitTrajectories)
                    {
                        ResourceUnitTrajectory trajectory = this.ResourceUnitTrajectories[resourceUnitIndex];
                        trajectory.AddYear();
                    }
                    if (logStandTrajectories)
                    {
                        SortedList<int, ResourceUnitTreeStatistics> resourceUnitStandStatisticsByID = model.Landscape.ResourceUnits[resourceUnitIndex].Trees.TreeStatisticsByStandID;
                        for (int resourceUnitStandIndex = 0; resourceUnitStandIndex < resourceUnitStandStatisticsByID.Count; ++resourceUnitStandIndex)
                        {
                            int standID = resourceUnitStandStatisticsByID.Keys[resourceUnitStandIndex];
                            ResourceUnitTreeStatistics resourceUnitStandStatistics = resourceUnitStandStatisticsByID.Values[resourceUnitStandIndex];

                            StandTreeStatistics standStatistics = this.currentYearStandStatistics[standID];
                            standStatistics.Add(resourceUnitStandStatistics);
                        }
                    }
                }
                for (int standIndex = 0; standIndex < this.currentYearStandStatistics.Count; ++standIndex)
                {
                    int standID = this.currentYearStandStatistics.Keys[standIndex];
                    StandTreeStatistics standStatistics = this.currentYearStandStatistics.Values[standIndex];
                    standStatistics.OnAdditionsComplete();
                    
                    Debug.Assert(standID == this.StandTrajectoriesByID.Keys[standIndex]);
                    StandTrajectory standTrajectory = this.StandTrajectoriesByID[standID];
                    standTrajectory.AddYear(standStatistics);

                    standStatistics.Zero(); // reset accumulation for next year
                }
            }

            // SQL
            if (this.sqlOutputs.Count > 0)
            {
                int currentSimulationYear = model.SimulationState.CurrentYear;
                if (this.sqlOutputTransaction == null)
                {
                    if (this.sqlDatabaseConnection == null)
                    {
                        throw new NotSupportedException("Attempt to call LogYear() without first calling Setup().");
                    }
                    this.sqlOutputTransaction = this.sqlDatabaseConnection.BeginTransaction();
                    this.mostRecentSimulationYearCommittedToDatabase = currentSimulationYear;
                }
                foreach (AnnualOutput output in this.sqlOutputs)
                {
                    output.LogYear(model, this.sqlOutputTransaction);
                }
                if (currentSimulationYear - this.mostRecentSimulationYearCommittedToDatabase > this.sqlCommitIntervalInYears)
                {
                    this.sqlOutputTransaction.Commit();
                    this.sqlOutputTransaction.Dispose();
                    this.sqlOutputTransaction = null;
                }
            }
        }
    }
}
