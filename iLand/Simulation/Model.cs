using iLand.Extensions;
using iLand.Input.ProjectFile;
using iLand.Tool;
using iLand.Tree;
using iLand.World;
using MaxRev.Gdal.Core;
using System;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Simulation
{
    public class Model : IDisposable
    {
        private bool isDisposed;
        private readonly MaybeParallel<ResourceUnit> resourceUnitParallel;

        public Landscape Landscape { get; private init; }
        public Management? Management { get; private init; }
        public Plugin.Modules Modules { get; private init; }
        public Output.Outputs Output { get; private init; }
        public Project Project { get; private init; }
        public RandomGenerator RandomGenerator { get; private init; }
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
            // setup GDAL if grid logging to GeoTiff is enabled
            if (projectFile.Output.Logging.HeightGrid.Enabled || projectFile.Output.Logging.LightGrid.Enabled)
            {
                // https://github.com/MaxRev-Dev/gdal.netcore - how to use
                GdalBase.ConfigureAll();
            }

            // setup of object model
            this.isDisposed = false;
            if (projectFile.Model.Settings.RandomSeed.HasValue)
            {
                this.RandomGenerator = new(mersenneTwister: true, projectFile.Model.Settings.RandomSeed.Value);
            }
            else
            {
                // if a random seed is null, RandomGenerator..ctor() generates one
                this.RandomGenerator = new(mersenneTwister: true);
            }

            this.Landscape = new(projectFile);
            this.Management = null;
            this.Modules = new();
            // construction of this.Output is deferred until trees have been loaded onto resource units
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

            // setup of trees
            TreePopulator treePopulator = new();
            treePopulator.SetupTrees(projectFile, this.Landscape, this.RandomGenerator);

            // setup the helper that does the multithreading
            this.resourceUnitParallel = new(this.Landscape.ResourceUnits)
            {
                MaximumThreads = this.Project.Model.Settings.MaxThreads
            };

            // initialize light pattern and then saplings and grass
            // Sapling and grass state depends on light pattern so overstory setup must complete before understory setup.
            this.ApplyAndReadLightPattern(); // requires this.ruParallel, will run multithreaded if enabled
            this.Landscape.SetupSaplingsAndGrass(projectFile, this.RandomGenerator);

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
            this.resourceUnitParallel.ForEach((ResourceUnit resourceUnit) =>
            {
                resourceUnit.SetupTreesAndSaplings(this.Landscape);
                resourceUnit.OnEndYear(); // call OnEndYear() to finalize initial resource unit and, if present, and stand statistics for logging
            });

            // log initial state in year zero
            this.Output = new(projectFile, this.Landscape, this);
            this.Output.LogYear(this);
        }

        private void ApplyAndReadLightPattern()
        {
            // intialize grids...
            this.ReinitializeLightGrid();

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
            this.resourceUnitParallel.ForEach((ResourceUnit resourceUnit) =>
            {
                ResourceUnitTrees treesOnResourceUnit = resourceUnit.Trees;
                if (this.Project.World.Geometry.IsTorus)
                {
                    // apply toroidal light pattern
                    int worldBufferWidth = this.Project.World.Geometry.BufferWidthInM;
                    int heightBufferTranslationInCells = worldBufferWidth / Constant.HeightCellSizeInM;
                    treesOnResourceUnit.CalculateDominantHeightFieldTorus(this.Landscape, heightBufferTranslationInCells);

                    int lightBufferTranslationInCells = worldBufferWidth / Constant.LightCellSizeInM;
                    treesOnResourceUnit.ApplyLightIntensityPatternTorus(this.Landscape, lightBufferTranslationInCells);

                    // read toroidal pattern: LIP value calculation
                    treesOnResourceUnit.ReadLightInfluenceFieldTorus(this.Landscape, lightBufferTranslationInCells); // multiplicative approach
                }
                else
                {
                    // apply light pattern
                    treesOnResourceUnit.CalculateDominantHeightField(this.Landscape);
                    treesOnResourceUnit.ApplyLightIntensityPattern(this.Landscape);

                    // read pattern: LIP value calculation
                    treesOnResourceUnit.ReadLightInfluenceField(this.Landscape); // multiplicative approach
                }
            });
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

        private void ReinitializeLightGrid() // initialize the LIF grid
        {
            // fill the whole grid with a value of 1.0
            this.Landscape.LightGrid.Fill(1.0F);

            // apply special values for grid cells border regions where out of area cells radiate into the main LIF grid
            const int lightOffset = Constant.LightCellsPerHeightCellWidth / 2; // for 5 px per height grid cell, the offset is 2
            const int maxRadiationDistanceInHeightCells = 7;
            float edgeInfluenceTaperCoefficient = 1.0F / maxRadiationDistanceInHeightCells;
            // int radiatingHeightCellCount = 0; // count of border height cells, maybe useful for debugging
            for (int heightGridIndex = 0; heightGridIndex < this.Landscape.VegetationHeightGrid.CellCount; ++heightGridIndex)
            {
                HeightCellFlags heightCellFlags = this.Landscape.VegetationHeightFlags[heightGridIndex];
                if (heightCellFlags.IsAdjacentToResourceUnit() == false)
                {
                    continue;
                }

                Point heightCellIndexXY = this.Landscape.VegetationHeightGrid.GetCellXYIndex(heightGridIndex);
                int minLightX = heightCellIndexXY.X * Constant.LightCellsPerHeightCellWidth - maxRadiationDistanceInHeightCells + lightOffset;
                int maxLightX = minLightX + 2 * maxRadiationDistanceInHeightCells + 1;
                int centerLightX = minLightX + maxRadiationDistanceInHeightCells;
                int minLightY = heightCellIndexXY.Y * Constant.LightCellsPerHeightCellWidth - maxRadiationDistanceInHeightCells + lightOffset;
                int maxLightY = minLightY + 2 * maxRadiationDistanceInHeightCells + 1;
                int centerLightY = minLightY + maxRadiationDistanceInHeightCells;
                for (int lightY = minLightY; lightY <= maxLightY; ++lightY)
                {
                    for (int lightX = minLightX; lightX <= maxLightX; ++lightX)
                    {
                        if (!this.Landscape.LightGrid.Contains(lightX, lightY) || !this.Landscape.VegetationHeightFlags[lightX, lightY, Constant.LightCellsPerHeightCellWidth].IsInResourceUnit())
                        {
                            continue;
                        }
                        float candidateLightValue = MathF.Max(MathF.Abs(lightX - centerLightX), MathF.Abs(lightY - centerLightY)) * edgeInfluenceTaperCoefficient;
                        float currentLightValue = this.Landscape.LightGrid[lightX, lightY];
                        if ((candidateLightValue >= 0.0F) && (currentLightValue > candidateLightValue))
                        {
                            this.Landscape.LightGrid[lightX, lightY] = candidateLightValue;
                        }
                    }
                }
                // ++radiatingHeightCellCount;
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
            ++this.SimulationState.CurrentCalendarYear;

            //this.GlobalSettings.SystemStatistics.Reset();
            // initalization at start of year for external modules
            this.Modules.OnStartYear();

            // execute scheduled events for the current year
            if (this.ScheduledEvents != null)
            {
                this.ScheduledEvents.RunYear(this);
            }
            // load the next year of the weather database
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
            this.resourceUnitParallel.ForEach((ResourceUnit resourceUnit) =>
            {
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

                for (int speciesIndex = 0; speciesIndex < resourceUnit.Trees.TreesBySpeciesID.Count; ++speciesIndex)
                {
                    TreeListSpatial treesOfSpecies = resourceUnit.Trees.TreesBySpeciesID.Values[speciesIndex];
                    treesOfSpecies.CalculateAnnualGrowth(this); // actual growth of individual trees
                }

                resourceUnit.Trees.AfterTreeGrowth();
            });

            this.Landscape.GrassCover.UpdateCoverage(this.Landscape, this.RandomGenerator); // evaluate the grass / herb cover (and its effect on regeneration)

            // regeneration
            if (this.Project.Model.Settings.RegenerationEnabled)
            {
                // seed dispersal
                foreach (TreeSpeciesSet speciesSet in this.Landscape.SpeciesSetsByTableName.Values)
                {
                    MaybeParallel<TreeSpecies> speciesParallel = new(speciesSet.ActiveSpecies);
                    speciesParallel.ForEach((TreeSpecies species) =>
                    {
                        Debug.Assert(species.SeedDispersal != null, "Attempt to disperse seeds from a tree species not configured for seed dispersal.");
                        species.SeedDispersal.DisperseSeeds(this);
                    });
                }
                this.resourceUnitParallel.ForEach((ResourceUnit resourceUnit) =>
                {
                    resourceUnit.EstablishSaplings(this);
                    resourceUnit.GrowSaplings(this);
                });
            }

            // external modules/disturbances
            // TODO: why do modules run after growth instead of before? can modules and management run sequentially so tree statistics
            // need only be recalculated once?
            this.Modules.RunYear();

            this.resourceUnitParallel.ForEach((ResourceUnit resourceUnit) =>
            {
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

            // create outputs
            this.Output.LogYear(this);
        }
    }
}
