using iLand.Input.ProjectFile;
using iLand.Input.Weather;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using MaxRev.Gdal.Core;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace iLand.Simulation
{
    public class Model : IDisposable
    {
        private bool isDisposed;
        private readonly Stopwatch stopwatch;

        public Landscape Landscape { get; private init; }
        public Management? Management { get; private init; }
        public Plugin.Modules Modules { get; private init; }
        public Output.Outputs Output { get; private init; }
        public ParallelOptions ParallelComputeOptions { get; private init; }
        public PerformanceCounters PerformanceCounters { get; private init; }
        public Project Project { get; private init; }
        public ThreadLocal<RandomGenerator> RandomGenerator { get; private init; }
        public ScheduledEvents? ScheduledEvents { get; private init; }
        public SimulationState SimulationState { get; private init; }

        public Model(Project projectFile)
        {
            // initialize tracing
            bool initialAutoFlushSetting = Trace.AutoFlush;
            string? logFileName = projectFile.Output.Logging.LogFile;
            TextWriterTraceListener? traceListener = null;
            if (logFileName != null)
            {
                string logFilePath = projectFile.GetFilePath(ProjectDirectory.Output, logFileName);
                traceListener = new TextWriterTraceListener(logFilePath);
                Trace.Listeners.Add(traceListener);
                if (Trace.AutoFlush != projectFile.Output.Logging.AutoFlush)
                {
                    Trace.AutoFlush = projectFile.Output.Logging.AutoFlush;
                }
            }

            this.stopwatch = new();
            this.stopwatch.Start();

            // setup GDAL if grid logging to GeoTiff is enabled
            if (projectFile.Output.Logging.HeightGrid.Enabled || projectFile.Output.Logging.LightGrid.Enabled)
            {
                // https://github.com/MaxRev-Dev/gdal.netcore - how to use
                GdalBase.ConfigureAll();
            }

            // setup of object model
            this.isDisposed = false;

            this.ParallelComputeOptions = new()
            {
                MaxDegreeOfParallelism = projectFile.Model.Settings.MaxComputeThreads
            };

            int randomSeed = projectFile.Model.Settings.RandomSeed ?? RandomNumberGenerator.GetInt32(Int32.MaxValue);
            this.RandomGenerator = new(() => { return new RandomGenerator(mersenneTwister: true, randomSeed); });

            this.Landscape = new(projectFile, this.ParallelComputeOptions);
            this.Management = null;
            this.Modules = new();
            // construction of this.Output is deferred until trees have been loaded onto resource units
            this.PerformanceCounters = new();
            this.Project = projectFile;
            this.ScheduledEvents = null;
            this.SimulationState = new(this.Landscape.WeatherFirstCalendarYear - 1)
            {
                TraceAutoFlushValueToRestore = initialAutoFlushSetting,
                TraceListener = traceListener
            };

            if (projectFile.World.Geometry.IsTorus && (this.Landscape.ResourceUnits.Count != 1))
            {
                throw new NotSupportedException("Toroidal light field indexing currently assumes only a single resource unit is present.");
            }

            this.PerformanceCounters.ObjectInstantiation = this.stopwatch.Elapsed;

            // setup of trees
            this.stopwatch.Restart();
            TreePopulator treePopulator = new();
            treePopulator.SetupTrees(projectFile, this.Landscape, this.ParallelComputeOptions, this.RandomGenerator);
            this.PerformanceCounters.TreeInstantiation = this.stopwatch.Elapsed;

            // initialize light pattern and then saplings and grass
            // Sapling and grass state depends on light pattern so overstory setup must complete before understory setup.
            this.ApplyAndReadLightPattern(); // requires this.ruParallel, will run multithreaded if enabled
            this.stopwatch.Restart();
            this.Landscape.SetupSaplingsAndGrass(projectFile, this.RandomGenerator.Value!);

            // setup of external modules
            this.Modules.SetupDisturbances();
            if (this.Modules.HasResourceUnitSetup())
            {
                for (int resourceUnitIndex = 0; resourceUnitIndex < this.Landscape.ResourceUnits.Count; ++resourceUnitIndex)
                {
                    ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                    this.Modules.SetupResourceUnit(resourceUnit);
                }
            }

            // (3) additional issues
            // (3.2) setup of regeneration
            if (this.Project.Model.Settings.RegenerationEnabled)
            {
                foreach (TreeSpeciesSet speciesSet in this.Landscape.SpeciesSetsByTableName.Values)
                {
                    speciesSet.SetupSeedDispersal(this);
                }
            }

            if (String.IsNullOrEmpty(this.Project.Model.Management.FileName) == false)
            {
                this.Management = new();
                // string mgmtFile = xml.GetString("model.management.file");
                // string path = this.GlobalSettings.Path(mgmtFile, "script");
            }

            // time series data
            string? scheduledEventsFileName = this.Project.Model.Settings.ScheduledEventsFileName;
            if (String.IsNullOrEmpty(scheduledEventsFileName) == false)
            {
                this.ScheduledEvents = new(this.Project, this.Project.GetFilePath(ProjectDirectory.Script, scheduledEventsFileName));
            }

            // calculate initial stand statistics
            Parallel.For(0, this.Landscape.ResourceUnits.Count, this.ParallelComputeOptions, (int resourceUnitIndex) =>
            {
                ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                resourceUnit.SetupTreesAndSaplings(this.Landscape);
                resourceUnit.OnEndYear(); // call OnEndYear() to finalize initial resource unit and, if present, and stand statistics for logging
            });

            this.PerformanceCounters.ObjectSetup = this.stopwatch.Elapsed;

            // log initial state in year zero
            this.stopwatch.Restart();
            this.Output = new(projectFile, this.Landscape, this.SimulationState, this.ParallelComputeOptions);
            this.Output.LogYear(this);
            this.PerformanceCounters.Logging += this.stopwatch.Elapsed;
        }

        private void ApplyAndReadLightPattern()
        {
            this.stopwatch.Restart();

            // fill the whole grid with a value of 1.0
            this.Landscape.LightGrid.Fill(1.0F);

            // reset height grid to the height of the regeneration layer, which is always assumed to be present
            // TODO: does this influence predictions following
            //   1) site prep, clearcut, and plant
            //   2) severe fire
            // where no vegetation of Constant.RegenerationLayerHeight is present?
            this.Landscape.VegetationHeightGrid.Fill(Constant.RegenerationLayerHeight);

            // current limitations of parallel resource unit evaluation:
            //
            // - Toroid indexing functions called below currently treat each resource unit as an individual torus and calculate height
            //   and light using only the (0, 0) resource area. This is problematic both for toroidal world size and thread safety. It
            //   can be addressed by changing the modulus calculations in Trees.*Torus().
            // - Light stamping does not lock light grid areas and race conditions therefore exist between threads stamping adjacent
            //   resource units.
            //
            if (this.Project.World.Geometry.IsTorus)
            {
                // apply toroidal light pattern
                int worldBufferWidth = this.Project.World.Geometry.BufferWidthInM;
                int heightBufferTranslationInCells = worldBufferWidth / Constant.HeightCellSizeInM;
                int lightBufferTranslationInCells = worldBufferWidth / Constant.LightCellSizeInM;
                Parallel.For(0, this.Landscape.ResourceUnits.Count, this.ParallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                    ResourceUnitTrees treesOnResourceUnit = resourceUnit.Trees;
                    treesOnResourceUnit.CalculateDominantHeightFieldTorus(this.Landscape, heightBufferTranslationInCells);
                    treesOnResourceUnit.ApplyLightIntensityPatternTorus(this.Landscape, lightBufferTranslationInCells);
                });

                // read toroidal pattern: LIP value calculation
                Parallel.For(0, this.Landscape.ResourceUnits.Count, this.ParallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                    ResourceUnitTrees treesOnResourceUnit = resourceUnit.Trees;
                    treesOnResourceUnit.ReadLightInfluenceFieldTorus(this.Landscape, lightBufferTranslationInCells); // multiplicative approach
                });
            }
            else
            {
                // apply light pattern
                Parallel.For(0, this.Landscape.ResourceUnits.Count, this.ParallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                    ResourceUnitTrees treesOnResourceUnit = resourceUnit.Trees;
                    treesOnResourceUnit.CalculateDominantHeightField(this.Landscape);
                    treesOnResourceUnit.ApplyLightIntensityPattern(this.Landscape);
                });
                // read pattern: LIP value calculation
                Parallel.For(0, this.Landscape.ResourceUnits.Count, this.ParallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                    ResourceUnitTrees treesOnResourceUnit = resourceUnit.Trees;
                    treesOnResourceUnit.ReadLightInfluenceField(this.Landscape); // multiplicative approach
                });
            }

            this.PerformanceCounters.LightPattern += stopwatch.Elapsed;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed == false)
            {
                if (disposing)
                {
                    this.Output.Dispose();
                    if (this.SimulationState.TraceListener != null)
                    {
                        Trace.Listeners.Remove(this.SimulationState.TraceListener);
                        Trace.AutoFlush = this.SimulationState.TraceAutoFlushValueToRestore;
                    }
                }
                this.isDisposed = true;
            }
        }

        /** Main model runner.
          The sequence of actions is as follows:
          (1) Load the weather of the new year
          (2) Reset statistics for resource unit as well as for dead/managed trees
          (3) Invoke Management.
          (4) *after* that, calculate Light patterns
          (5) 3-PG on stand level, tree growth. Clear stand-statistcs before they are filled by single-tree-growth. calculate water cycle (with LAIs before management)
          (6) execute Regeneration
          (7) invoke disturbance modules
          (8) calculate carbon cycle
          (9) calculate statistics for the year
          (10) write database outputs
          */
        public void RunYear() // run a single year
        {
            this.stopwatch.Restart();
            ++this.SimulationState.CurrentCalendarYear;

            //this.GlobalSettings.SystemStatistics.Reset();
            // initalization at start of year for external modules
            this.Modules.OnStartYear();

            // execute scheduled events for the current year
            if (this.ScheduledEvents != null)
            {
                this.ScheduledEvents.RunYear(this);
            }
            // move CO₂ and weather time series to this year
            CO2TimeSeriesMonthly co2byMonth = this.Landscape.CO2ByMonth;
            co2byMonth.CurrentYearStartIndex += Constant.MonthsInYear;
            co2byMonth.NextYearStartIndex += Constant.MonthsInYear;
            if (co2byMonth.NextYearStartIndex >= co2byMonth.Count)
            {
                throw new NotSupportedException("CO₂ for calendar year " + this.SimulationState.CurrentCalendarYear + " is not present in the CO₂ time series '" + this.Project.World.Weather.CO2File + ".");
            }
            foreach (World.Weather weather in this.Landscape.WeatherByID.Values)
            {
                weather.OnStartYear(this);
            }

            // reset tree statistics
            foreach (ResourceUnit resourceUnit in this.Landscape.ResourceUnits)
            {
                resourceUnit.OnStartYear();
            }
            foreach (TreeSpeciesSet speciesSet in this.Landscape.SpeciesSetsByTableName.Values)
            {
                speciesSet.OnStartYear(this);
            }
            // management
            if (this.Management != null)
            {
                this.Management.RunYear();
            }

            // if management harvested trees, created snags, or dropped trees for dead wood recruitment, removed these trees from the
            // live tree lists and recalculate tree statistics (updated LAI per species is needed later in production)
            foreach (ResourceUnit resourceUnit in this.Landscape.ResourceUnits)
            {
                if (resourceUnit.Trees.HasDeadTrees)
                {
                    resourceUnit.Trees.RemoveDeadTrees();
                    resourceUnit.Trees.RecalculateStatistics(zeroSaplingStatistics: true);
                }
            }

            this.PerformanceCounters.OnStartYear += stopwatch.Elapsed;

            // process a cycle of individual growth
            // create light influence patterns and readout light state of individual trees
            this.ApplyAndReadLightPattern(); // will run multithreaded if enabled

            /** Main function for the growth of stands and trees.
               This includes several steps.
               (1) calculate the stocked area (i.e. count pixels in height grid)
               (2) 3-PG production (including growth modifier calculation and water cycle)
               (3) single tree growth (including mortality)
               (4) cleanup of tree lists (remove dead trees)
              */
            // let the trees grow (growth on stand-level, tree-level, mortality)
            this.stopwatch.Restart();
            Parallel.For(0, this.Landscape.ResourceUnits.Count, this.ParallelComputeOptions, (int resourceUnitIndex) =>
            {
                ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                // stocked area
                resourceUnit.CountHeightCellsContainingTreesTallerThanTheRegenerationLayer(this.Landscape);
                // 3-PG tree growth
                resourceUnit.CalculateWaterAndBiomassGrowthForYear(this);

                resourceUnit.Trees.BeforeTreeGrowth(); // reset aging
                // calculate light responses
                // responses are based on *modified* values for LightResourceIndex
                for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
                {
                    TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        treesOfSpecies.CalculateLightResponse(treeIndex);
                    }
                }

                resourceUnit.Trees.CalculatePhotosyntheticActivityRatio();

                RandomGenerator random = this.RandomGenerator.Value!;
                for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
                {
                    TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                    treesOfSpecies.CalculateAnnualGrowth(this, random); // actual growth of individual trees
                }

                resourceUnit.Trees.AfterTreeGrowth();
            });

            this.Landscape.GrassCover.UpdateCoverage(this.Landscape, this.RandomGenerator.Value!); // evaluate the grass / herb cover (and its effect on regeneration)

            // regeneration
            if (this.Project.Model.Settings.RegenerationEnabled)
            {
                // seed dispersal
                foreach (TreeSpeciesSet speciesSet in this.Landscape.SpeciesSetsByTableName.Values)
                {
                    Parallel.For(0, speciesSet.ActiveSpecies.Count, this.ParallelComputeOptions, (int speciesIndex) =>
                    {
                        TreeSpecies species = speciesSet.ActiveSpecies[speciesIndex];
                        Debug.Assert(species.SeedDispersal != null, "Attempt to disperse seeds from a tree species not configured for seed dispersal.");
                        species.SeedDispersal.DisperseSeeds(this);
                    });
                }
                Parallel.For(0, this.Landscape.ResourceUnits.Count, this.ParallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                    resourceUnit.EstablishSaplings(this);
                    resourceUnit.GrowSaplings(this);
                });
            }

            // external modules/disturbances
            // TODO: why do modules run after growth instead of before? can modules and management run sequentially so tree statistics
            // need only be recalculated once?
            this.Modules.RunYear();

            Parallel.For(0, this.Landscape.ResourceUnits.Count, this.ParallelComputeOptions, (int resourceUnitIndex) =>
            {
                ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                // clean tree lists again and recalculate statistcs if external modules removed trees
                if (resourceUnit.Trees.HasDeadTrees)
                {
                    resourceUnit.Trees.RemoveDeadTrees();
                    resourceUnit.Trees.RecalculateStatistics(zeroSaplingStatistics: false);
                }

                // calculate soil / snag dynamics
                if (this.Project.Model.Settings.CarbonCycleEnabled)
                {
                    // (1) do calculations on snag dynamics for the resource unit
                    // (2) do the soil carbon and nitrogen dynamics calculations (ICBM/2N)
                    resourceUnit.CalculateCarbonCycle();
                }

                // calculate statistics
                resourceUnit.OnEndYear();
            });

            this.PerformanceCounters.TreeGrowthAndMortality += this.stopwatch.Elapsed;

            // create outputs
            this.stopwatch.Restart();
            this.Output.LogYear(this);
            this.PerformanceCounters.Logging += this.stopwatch.Elapsed;
        }
    }
}
