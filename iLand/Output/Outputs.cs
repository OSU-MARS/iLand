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
        private int mostRecentSimulationYearCommittedToDatabase;
        private bool isDisposed;
        private readonly int sqlCommitIntervalInYears;
        private readonly SqliteConnection? sqlDatabaseConnection;
        private readonly List<AnnualOutput> sqlOutputs;
        private SqliteTransaction? sqlOutputTransaction;
        private readonly ResourceUnitToStandStatisticsConverter[] standStatisticsByPartition;
        private readonly SortedList<int, StandLiveTreeAndSaplingStatistics> standStatisticsForCurrentYear;

        public LandscapeRemovedAnnualOutput? LandscapeRemovedSql { get; private init; }
        public ResourceUnitTrajectory[] ResourceUnitTrajectories { get; private init; } // in same order as Landscape.ResourceUnits, array for simplicity of multithreaded population
        public SortedList<int, StandTrajectory> StandTrajectoriesByID { get; private init; }
        public TreeRemovedAnnualOutput? TreeRemovedSql { get; private init; }

        public Outputs(Project projectFile, Landscape landscape, SimulationState simulationState)
        {
            this.mostRecentSimulationYearCommittedToDatabase = -1;
            this.isDisposed = false;
            this.sqlCommitIntervalInYears = 10; // 
            this.sqlDatabaseConnection = null; // initialized in Setup()
            this.sqlOutputs = new();
            this.sqlOutputTransaction = null; // managed in LogYear()
            this.standStatisticsByPartition = Array.Empty<ResourceUnitToStandStatisticsConverter>();
            this.standStatisticsForCurrentYear = new();

            this.LandscapeRemovedSql = null;
            this.ResourceUnitTrajectories = Array.Empty<ResourceUnitTrajectory>();
            this.StandTrajectoriesByID = new();
            this.TreeRemovedSql = null;

            // memory outputs
            ResourceUnitOutputs resourceUnitMemoryOutputs = projectFile.Output.Memory.ResourceUnits;
            bool logAnyTypeOfResourceUnitTrajectory = resourceUnitMemoryOutputs.IsAnyOutputEnabled();
            bool logStandTrajectories = projectFile.Output.Memory.StandTrajectories.Enabled;
            if (logAnyTypeOfResourceUnitTrajectory || logStandTrajectories)
            {
                IList<ResourceUnit> resourceUnits = landscape.ResourceUnits;
                int resourceUnitCount = landscape.ResourceUnits.Count;
                if (logAnyTypeOfResourceUnitTrajectory)
                {
                    this.ResourceUnitTrajectories = new ResourceUnitTrajectory[resourceUnitCount];
                }
                if (logStandTrajectories)
                {
                    int maxThreads = simulationState.ParallelComputeOptions.MaxDegreeOfParallelism;
                    this.standStatisticsByPartition = new ResourceUnitToStandStatisticsConverter[maxThreads];
                    for (int partitionIndex = 0; partitionIndex < maxThreads; ++partitionIndex)
                    {
                        this.standStatisticsByPartition[partitionIndex] = new();
                    }
                }

                int initialCapacityInYears = projectFile.Output.Memory.InitialTrajectoryLengthInYears;
                ParallelOptions parallelComputeOptions = simulationState.ParallelComputeOptions;
                (int partitions, int resourceUnitsPerPartition) = parallelComputeOptions.GetUniformPartitioning(resourceUnitCount, Constant.Data.MinimumResourceUnitsPerLoggingThread);
                Parallel.For(0, partitions, parallelComputeOptions, (int partitionIndex) =>
                {
                    (int startResourceUnitIndex, int endResourceUnitIndex) = ParallelOptionsExtensions.GetUniformPartitionRange(partitionIndex, resourceUnitsPerPartition, resourceUnitCount);
                    for (int resourceUnitIndex = startResourceUnitIndex; resourceUnitIndex < endResourceUnitIndex; ++resourceUnitIndex)
                    {
                        ResourceUnit resourceUnit = resourceUnits[resourceUnitIndex];
                        if (logAnyTypeOfResourceUnitTrajectory)
                        {
                            // create a trajectory for each reasource unit
                            this.ResourceUnitTrajectories[resourceUnitIndex] = new ResourceUnitTrajectory(resourceUnit, resourceUnitMemoryOutputs, initialCapacityInYears);
                        }
                        if (logStandTrajectories)
                        {
                            // create a trajectory for each stand
                            // As only one trajectory need be created per stand ID, stand ID differencing is used to limit the number of
                            // ContainsKey() calls and locks taken.
                            IList<TreeListSpatial> treesBySpecies = resourceUnit.Trees.TreesBySpeciesID.Values;
                            int previousStandID = Constant.DefaultStandID;
                            for (int treeSpeciesIndex = 0; treeSpeciesIndex < treesBySpecies.Count; ++treeSpeciesIndex)
                            {
                                TreeListSpatial treesOfSpecies = treesBySpecies[treeSpeciesIndex];
                                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                                {
                                    int standID = treesOfSpecies.StandID[treeIndex];
                                    if (standID != previousStandID)
                                    {
                                        if (this.standStatisticsForCurrentYear.ContainsKey(standID) == false)
                                        {
                                            lock (this.standStatisticsForCurrentYear)
                                            {
                                                if (this.standStatisticsForCurrentYear.ContainsKey(standID) == false)
                                                {
                                                    this.standStatisticsForCurrentYear.Add(standID, new StandLiveTreeAndSaplingStatistics(standID));
                                                    this.StandTrajectoriesByID.Add(standID, new StandTrajectory(standID, initialCapacityInYears));
                                                }
                                            }
                                        }

                                        previousStandID = standID;
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
            // If needed grid export can be made multithreaded but, for now, it's assumed not to be performance critical.
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
            ParallelOptions parallelComputeOptions = model.SimulationState.ParallelComputeOptions;
            if (logResourceUnitTrajectories || logStandTrajectories)
            {
                int resourceUnitTrajectoryCount = this.ResourceUnitTrajectories.Length;
                (int partitions, int resourceUnitsPerPartition) = parallelComputeOptions.GetUniformPartitioning(resourceUnitTrajectoryCount, Constant.Data.MinimumResourceUnitsPerLoggingThread);
                Parallel.For(0, partitions, parallelComputeOptions, (int partitionIndex) =>
                {
                    (int startTrajectoryIndex, int endTrajectoryIndex) = ParallelOptionsExtensions.GetUniformPartitionRange(partitionIndex, resourceUnitsPerPartition, resourceUnitTrajectoryCount);
                    ResourceUnitToStandStatisticsConverter? standStatisticsConverter = logStandTrajectories ? this.standStatisticsByPartition[partitionIndex] : null;

                    for (int resourceUnitIndex = startTrajectoryIndex; resourceUnitIndex < endTrajectoryIndex; ++resourceUnitIndex)
                    {
                        ResourceUnitTrajectory trajectory = this.ResourceUnitTrajectories[resourceUnitIndex];
                        if (logResourceUnitTrajectories)
                        {
                            trajectory.AddYear();
                        }
                        if (logStandTrajectories)
                        {
                            Debug.Assert(standStatisticsConverter != null);
                            ResourceUnit resourceUnit = trajectory.ResourceUnit;
                            standStatisticsConverter.CalculateStandStatisticsFromResourceUnit(resourceUnit);
                            standStatisticsConverter.AddResourceUnitToStandStatisticsThreadSafe(resourceUnit.AreaInLandscapeInM2, this.standStatisticsForCurrentYear);
                        }
                    }
                });

                int standUnitTrajectoryCount = this.standStatisticsForCurrentYear.Count;
                (partitions, int standsPerPartition) = parallelComputeOptions.GetUniformPartitioning(standUnitTrajectoryCount, Constant.Data.MinimumStandsPerLoggingThread);
                Parallel.For(0, partitions, parallelComputeOptions, (int partitionIndex) =>
                {
                    (int startStandIndex, int endStandIndex) = ParallelOptionsExtensions.GetUniformPartitionRange(partitionIndex, standsPerPartition, standUnitTrajectoryCount);
                    for (int standIndex = startStandIndex; standIndex < endStandIndex; ++standIndex)
                    {
                        int standID = this.standStatisticsForCurrentYear.Keys[standIndex];
                        StandLiveTreeAndSaplingStatistics standStatistics = this.standStatisticsForCurrentYear.Values[standIndex];
                        standStatistics.OnAdditionsComplete();

                        Debug.Assert(standID == this.StandTrajectoriesByID.Keys[standIndex]);
                        StandTrajectory standTrajectory = this.StandTrajectoriesByID[standID];
                        standTrajectory.AddYear(standStatistics);

                        standStatistics.Zero(); // reset accumulation for next year
                    }
                });
            }

            // SQL outputs
            // Outputs share a single transaction because, while SQLite supports multiple concurrent readers, writes are limited to a
            // single transaction and BeginTransaction() and Execute*() calls block until other pending changes commit (see
            // https://docs.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions).
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
                    // single threaded for now
                    // If this is made parallel each output will need to lock the transaction on each call to insertRow.ExecuteNonQuery().
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
