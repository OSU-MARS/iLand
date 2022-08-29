using iLand.Extensions;
using iLand.Input.ProjectFile;
using iLand.Output.Memory;
using iLand.Output.Sql;
using iLand.Simulation;
using iLand.Tree;
using iLand.World;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
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

        public LandscapeRemovedAnnualOutput? LandscapeRemovedSql { get; private init; }
        public ResourceUnitTrajectory[] ResourceUnitTrajectories { get; private init; } // in same order as Landscape.ResourceUnits, array for simplicity of multithreaded population
        public SortedList<int, StandTrajectory> StandTrajectoriesByID { get; private init; }
        public TreeRemovedAnnualOutput? TreeRemovedSql { get; private init; }

        public Outputs(Project projectFile, Landscape landscape, SimulationState simulationState, ParallelOptions parallelComputeOptions)
        {
            this.currentYearStandStatistics = new();
            this.mostRecentSimulationYearCommittedToDatabase = -1;
            this.isDisposed = false;
            this.sqlCommitIntervalInYears = 10; // 
            this.sqlDatabaseConnection = null; // initialized in Setup()
            this.sqlOutputs = new();
            this.sqlOutputTransaction = null; // managed in LogYear()

            this.LandscapeRemovedSql = null;
            this.ResourceUnitTrajectories = Array.Empty<ResourceUnitTrajectory>();
            this.StandTrajectoriesByID = new();
            this.TreeRemovedSql = null;

            // memory outputs
            ResourceUnitMemoryOutputs resourceUnitMemoryOutputs = projectFile.Output.Memory.ResourceUnitTrajectories;
            bool logAnyTypeOfResourceUnitTrajectory = resourceUnitMemoryOutputs != ResourceUnitMemoryOutputs.None;
            bool logStandTrajectories = projectFile.Output.Memory.StandTrajectories.Enabled;
            if (logAnyTypeOfResourceUnitTrajectory || logStandTrajectories)
            {
                IList<ResourceUnit> resourceUnits = landscape.ResourceUnits;
                int resourceUnitCount = landscape.ResourceUnits.Count;
                if (logAnyTypeOfResourceUnitTrajectory)
                {
                    this.ResourceUnitTrajectories = new ResourceUnitTrajectory[resourceUnitCount];
                }

                int initialCapacityInYears = projectFile.Output.Memory.InitialTrajectoryLengthInYears;
                (int partitions, int resourceUnitsPerPartition) = parallelComputeOptions.GetUniformPartitioning(resourceUnitCount, Constant.Data.MinimumResourceUnitsPerLoggingThread);
                Parallel.For(0, partitions, parallelComputeOptions, (int partitionIndex) =>
                {
                    (int startResourceUnitIndex, int endResourceUnitIndex) = ParallelOptionsExtensions.GetUniformPartitionRange(partitionIndex, resourceUnitsPerPartition, resourceUnitCount);
                    for (int resourceUnitIndex = startResourceUnitIndex; resourceUnitIndex < endResourceUnitIndex; ++resourceUnitIndex)
                    {
                        ResourceUnit resourceUnit = resourceUnits[resourceUnitIndex];
                        if (logAnyTypeOfResourceUnitTrajectory)
                        {
                            this.ResourceUnitTrajectories[resourceUnitIndex] = new ResourceUnitTrajectory(resourceUnit, resourceUnitMemoryOutputs, initialCapacityInYears);
                        }
                        if (logStandTrajectories)
                        {
                            SortedList<int, ResourceUnitTreeStatistics> standsInResourceUnit = resourceUnit.Trees.TreeStatisticsByStandID;
                            for (int standIndex = 0; standIndex < standsInResourceUnit.Count; ++standIndex)
                            {
                                int standID = standsInResourceUnit.Keys[standIndex];
                                if (this.currentYearStandStatistics.ContainsKey(standID) == false)
                                {
                                    lock (this.currentYearStandStatistics)
                                    {
                                        if (this.currentYearStandStatistics.ContainsKey(standID) == false)
                                        {
                                            this.currentYearStandStatistics.Add(standID, new StandTreeStatistics(standID));
                                            this.StandTrajectoriesByID.Add(standID, new StandTrajectory(standID, initialCapacityInYears));
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }

            // SQL outputs
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
                this.LandscapeRemovedSql = new LandscapeRemovedAnnualOutput();
                this.sqlOutputs.Add(this.LandscapeRemovedSql);
            }
            if (sqlOutputSettings.ThreePG.Enabled)
            {
                this.sqlOutputs.Add(new ThreePGMonthlyOutput());
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
            if (sqlOutputSettings.IndividualTree.Enabled)
            {
                this.sqlOutputs.Add(new IndividualTreeAnnualOutput());
            }
            if (sqlOutputSettings.TreeRemoved.Enabled)
            {
                this.TreeRemovedSql = new TreeRemovedAnnualOutput();
                this.sqlOutputs.Add(this.TreeRemovedSql);
            }
            if (sqlOutputSettings.Water.Enabled)
            {
                this.sqlOutputs.Add(new WaterAnnualOutput());
            }

            // open output SQL database if there are SQL outputs
            if (this.sqlOutputs.Count > 0)
            {
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
                for (int outputIndex = 0; outputIndex < sqlOutputs.Count; ++outputIndex)
                {
                    AnnualOutput output = this.sqlOutputs[outputIndex];
                    output.Setup(projectFile, simulationState);
                    output.Open(outputTableCreationTransaction);
                }
                outputTableCreationTransaction.Commit();
            }
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
            // log files
            if (model.Project.Output.Logging.HeightGrid.Enabled)
            {
                string? coordinateSystem = model.Project.Model.Settings.CoordinateSystem;
                Debug.Assert(coordinateSystem != null); // should be guaranteed by project deserialization
                string heightGridFilePath = model.Project.GetFilePath(ProjectDirectory.Output, "height grid " + model.SimulationState.CurrentCalendarYear + ".tif");
                model.Landscape.VegetationHeightGrid.ExportToGeoTiff(heightGridFilePath, coordinateSystem, model.Landscape.ProjectOriginInGisCoordinates);
            }
            if (model.Project.Output.Logging.LightGrid.Enabled)
            {
                string? coordinateSystem = model.Project.Model.Settings.CoordinateSystem;
                Debug.Assert(coordinateSystem != null); // should be guaranteed by project deserialization
                string lightGridFilePath = model.Project.GetFilePath(ProjectDirectory.Output, "light grid " + model.SimulationState.CurrentCalendarYear + ".tif");
                model.Landscape.LightGrid.ExportToGeoTiff(lightGridFilePath, coordinateSystem, model.Landscape.ProjectOriginInGisCoordinates);
            }

            // memory outputs
            // Partitioning advantage is minor to negligible.
            bool logResourceUnitTrajectories = this.ResourceUnitTrajectories.Length > 0;
            bool logStandTrajectories = this.StandTrajectoriesByID.Count > 0;
            if (logResourceUnitTrajectories || logStandTrajectories)
            {
                int resourceUnitTrajectoryCount = this.ResourceUnitTrajectories.Length;
                (int partitions, int resourceUnitsPerPartition) = model.ParallelComputeOptions.GetUniformPartitioning(resourceUnitTrajectoryCount, Constant.Data.MinimumResourceUnitsPerLoggingThread);
                Parallel.For(0, partitions, model.ParallelComputeOptions, (int partitionIndex) =>
                {
                    (int startTrajectoryIndex, int endTrajectoryIndex) = ParallelOptionsExtensions.GetUniformPartitionRange(partitionIndex, resourceUnitsPerPartition, resourceUnitTrajectoryCount);
                    for (int resourceUnitIndex = startTrajectoryIndex; resourceUnitIndex < endTrajectoryIndex; ++resourceUnitIndex)
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
                });

                int standUnitTrajectoryCount = this.currentYearStandStatistics.Count;
                (partitions, int standsPerPartition) = model.ParallelComputeOptions.GetUniformPartitioning(standUnitTrajectoryCount, Constant.Data.MinimumStandsPerLoggingThread);
                Parallel.For(0, partitions, model.ParallelComputeOptions, (int partitionIndex) =>
                {
                    (int startStandIndex, int endStandIndex) = ParallelOptionsExtensions.GetUniformPartitionRange(partitionIndex, standsPerPartition, standUnitTrajectoryCount);
                    for (int standIndex = startStandIndex; standIndex < endStandIndex; ++standIndex)
                    {
                        int standID = this.currentYearStandStatistics.Keys[standIndex];
                        StandTreeStatistics standStatistics = this.currentYearStandStatistics.Values[standIndex];
                        standStatistics.OnAdditionsComplete();

                        Debug.Assert(standID == this.StandTrajectoriesByID.Keys[standIndex]);
                        StandTrajectory standTrajectory = this.StandTrajectoriesByID[standID];
                        standTrajectory.AddYear(standStatistics);

                        standStatistics.Zero(); // reset accumulation for next year
                    }
                });
            }

            // SQL outputs
            if (this.sqlOutputs.Count > 0)
            {
                int currentCalendarYear = model.SimulationState.CurrentCalendarYear;
                if (this.sqlOutputTransaction == null)
                {
                    if (this.sqlDatabaseConnection == null)
                    {
                        throw new NotSupportedException("Attempt to call LogYear() without first calling Setup().");
                    }
                    this.sqlOutputTransaction = this.sqlDatabaseConnection.BeginTransaction();
                    this.mostRecentSimulationYearCommittedToDatabase = currentCalendarYear;
                }
                foreach (AnnualOutput output in this.sqlOutputs)
                {
                    output.LogYear(model, this.sqlOutputTransaction);
                }
                if (currentCalendarYear - this.mostRecentSimulationYearCommittedToDatabase > this.sqlCommitIntervalInYears)
                {
                    this.sqlOutputTransaction.Commit();
                    this.sqlOutputTransaction.Dispose();
                    this.sqlOutputTransaction = null;
                }
            }
        }
    }
}
