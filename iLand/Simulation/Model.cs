using iLand.Input.ProjectFile;
using iLand.Input.Weather;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using MaxRev.Gdal.Core;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
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
        public PerformanceCounters PerformanceCounters { get; private init; }
        public Project Project { get; private init; }
        public ThreadLocal<RandomGenerator> RandomGenerator { get; private init; }
        public ScheduledEvents? ScheduledEvents { get; private init; }
        public SimulationState SimulationState { get; private init; }
        public SvdStates? SvdStates { get; private init; }

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

            if ((projectFile.Model.Settings.SimdWidth > 32) && (Avx2.IsSupported == false))
            {
                throw new ArgumentOutOfRangeException(nameof(projectFile), "simdWidth in project file is " + projectFile.Model.Settings.SimdWidth + " bits but AVX2 instructions are not supported. Either set the SIMD width to 32 bits or run on a processor with the AVX2 instruction set.");
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
            int randomSeed = projectFile.Model.Settings.RandomSeed ?? RandomNumberGenerator.GetInt32(Int32.MaxValue);
            this.RandomGenerator = new(() => { return new RandomGenerator(mersenneTwister: true, randomSeed); });

            ParallelOptions parallelComputeOptions = new()
            {
                MaxDegreeOfParallelism = projectFile.Model.Settings.MaxComputeThreads
            };
            this.Landscape = new(projectFile, parallelComputeOptions);
            this.Management = null;
            this.Modules = new();
            // construction of this.Output is deferred until trees have been loaded onto resource units
            this.PerformanceCounters = new();
            this.Project = projectFile;
            this.ScheduledEvents = null;
            this.SimulationState = new(this.Landscape.WeatherFirstCalendarYear - 1, parallelComputeOptions)
            {
                TraceAutoFlushValueToRestore = initialAutoFlushSetting,
                TraceListener = traceListener
            };
            this.SvdStates = null;

            if (projectFile.World.Geometry.IsTorus && (this.Landscape.ResourceUnits.Count != 1))
            {
                throw new NotSupportedException("Toroidal light field indexing currently assumes only a single resource unit is present.");
            }

            this.PerformanceCounters.ObjectInstantiation = this.stopwatch.Elapsed;

            // setup of trees
            this.stopwatch.Restart();
            TreePopulator treePopulator = new();
            treePopulator.SetupTrees(projectFile, this.Landscape, parallelComputeOptions, this.RandomGenerator);
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
            Parallel.For(0, this.Landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
            {
                ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                resourceUnit.SetupTreesAndSaplings(this.Landscape);
                resourceUnit.OnEndYear(this); // call OnEndYear() to finalize initial resource unit and, if present, and stand statistics for logging
            });

            this.PerformanceCounters.ObjectSetup = this.stopwatch.Elapsed;

            // log initial state in year zero
            this.stopwatch.Restart();
            this.Output = new(projectFile, this.Landscape, this.SimulationState);
            this.Output.LogYear(this);
            this.PerformanceCounters.Logging += this.stopwatch.Elapsed;
        }

        private void ApplyAndReadLightPattern() // C++: Model::applyPattern()
        {
            this.stopwatch.Restart();

            // reset height grid to the height of the regeneration layer, which is always assumed to be present
            // TODO: does this influence predictions following
            //   1) site prep, clearcut, and plant
            //   2) severe fire
            // where no vegetation of Constant.RegenerationLayerHeight is present?
            Landscape landscape = this.Landscape;
            landscape.VegetationHeightGrid.Fill(Constant.RegenerationLayerHeight);

            // reset light grid to no shade
            // If necessary, this can be moved into CalculateDominantHeightField*().
            this.Landscape.LightGrid.Fill(Constant.Grid.FullLightIntensity);
            this.PerformanceCounters.LightFill = stopwatch.Elapsed; // fills are fast but done single threaded

            // current limitations of parallel resource unit evaluation:
            //
            // - Toroid indexing functions called below currently treat each resource unit as an individual torus and calculate height
            //   and light using only the (0, 0) resource area. This is problematic both for toroidal world size and thread safety. It
            //   can be addressed by changing the modulus calculations in Trees.*Torus().
            // - Light stamping does not lock light grid areas and race conditions therefore exist between threads stamping adjacent
            //   resource units.
            //
            // Explicit resource unit partitioning shows no profiling advantage over Parallel.For()'s default behavior.
            this.stopwatch.Restart();
            ConcurrentQueue<DominantHeightBuffer> dominantHeightBuffers = this.SimulationState.DominantHeightBuffers;
            ConcurrentQueue<LightBuffer> lightBuffers = this.SimulationState.LightBuffers;
            ParallelOptions parallelComputeOptions = this.SimulationState.ParallelComputeOptions;
            if (this.Project.World.Geometry.IsTorus)
            {
                // apply toroidal light pattern
                Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnitTrees treesOnResourceUnit = landscape.ResourceUnits[resourceUnitIndex].Trees;
                    treesOnResourceUnit.CalculateDominantHeightFieldTorus(landscape, dominantHeightBuffers);
                });
                Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnitTrees treesOnResourceUnit = landscape.ResourceUnits[resourceUnitIndex].Trees;
                    treesOnResourceUnit.ApplyLightIntensityPatternTorus(landscape, lightBuffers);
                });

                // read toroidal pattern: LIP value calculation
                Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnitTrees treesOnResourceUnit = landscape.ResourceUnits[resourceUnitIndex].Trees;
                    treesOnResourceUnit.ReadLightInfluenceFieldTorus(landscape); // multiplicative approach
                });
            }
            else
            {
                // apply light pattern
                Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnitTrees treesOnResourceUnit = landscape.ResourceUnits[resourceUnitIndex].Trees;
                    treesOnResourceUnit.CalculateDominantHeightField(landscape, dominantHeightBuffers);
                });
                switch (this.Project.Model.Settings.SimdWidth)
                {
                    case 32:
                        Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                        {
                            ResourceUnitTrees treesOnResourceUnit = landscape.ResourceUnits[resourceUnitIndex].Trees;
                            treesOnResourceUnit.ApplyLightIntensityPattern(landscape, lightBuffers);
                        });
                        break;
                    case 128:
                        Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                        {
                            ResourceUnitTrees treesOnResourceUnit = landscape.ResourceUnits[resourceUnitIndex].Trees;
                            treesOnResourceUnit.ApplyLightIntensityPatternVex128(landscape, lightBuffers);
                        });
                        break;
                    case 256:
                        Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                        {
                            ResourceUnitTrees treesOnResourceUnit = landscape.ResourceUnits[resourceUnitIndex].Trees;
                            treesOnResourceUnit.ApplyLightIntensityPatternAvx(landscape, lightBuffers);
                        });
                        break;
                    default:
                        throw new NotSupportedException("Unhandled SIMD width of " + this.Project.Model.Settings.SimdWidth + " bits.");
                }

                // read pattern: LIP value calculation
                Parallel.For(0, landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnitTrees treesOnResourceUnit = landscape.ResourceUnits[resourceUnitIndex].Trees;
                    treesOnResourceUnit.ReadLightInfluenceField(landscape); // multiplicative approach
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

        /** Main model run routine.
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
        public void RunYear() // run a single year, C++: Model::runYear()
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
            co2byMonth.CurrentYearStartIndex += Constant.Time.MonthsInYear;
            co2byMonth.NextYearStartIndex += Constant.Time.MonthsInYear;
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
                resourceUnit.OnStartYear(this.Landscape);
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
            ParallelOptions parallelComputeOptions = this.SimulationState.ParallelComputeOptions;
            Parallel.For(0, this.Landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
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
                    Parallel.For(0, speciesSet.ActiveSpecies.Count, parallelComputeOptions, (int speciesIndex) =>
                    {
                        TreeSpecies species = speciesSet.ActiveSpecies[speciesIndex];
                        Debug.Assert(species.SeedDispersal != null, "Attempt to disperse seeds from a tree species not configured for seed dispersal.");
                        species.SeedDispersal.DisperseSeeds(this.Project, this.RandomGenerator.Value!);
                    });
                }

                foreach (TreeSpeciesSet speciesSet in this.Landscape.SpeciesSetsByTableName.Values)
                {
                    // the sapling seed maps are cleared before sapling growth (where sapling seed maps are filled)
                    // the content of the seed maps is used in the *next* year
                    speciesSet.ClearSaplingSeedMap();
                }

                Parallel.For(0, this.Landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
                {
                    ResourceUnit resourceUnit = this.Landscape.ResourceUnits[resourceUnitIndex];
                    resourceUnit.EstablishSaplings(this);
                    resourceUnit.GrowSaplings(this);
                    resourceUnit.UpdateSaplingCellGrassCover(this);
                });
            }

            // external modules/disturbances
            // TODO: why do modules run after growth instead of before? can modules and management run sequentially so tree statistics
            // need only be recalculated once?
            this.Modules.RunYear();

            Parallel.For(0, this.Landscape.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
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
                    resourceUnit.CalculateCarbonCycle(this);
                }

                // calculate statistics
                resourceUnit.OnEndYear(this);
            });

            this.PerformanceCounters.TreeGrowthAndMortality += this.stopwatch.Elapsed;

            // create outputs
            this.stopwatch.Restart();
            this.Output.LogYear(this);
            this.PerformanceCounters.Logging += this.stopwatch.Elapsed;
        }
    }
}
