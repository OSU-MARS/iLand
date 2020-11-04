using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tools;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Xml;

namespace iLand.Simulation
{
    public class Model : IDisposable
    {
        private bool isDisposed;
        private readonly MaybeParallel<ResourceUnit> ruParallel;

        public int CurrentYear { get; set; }

        // TODO; move FileLocation APIs to project
        public FileLocations Files { get; private set; }
        public Landscape Landscape { get; private set; }
        public Management Management { get; private set; }
        public ModelSettings ModelSettings { get; private set; }
        public Plugin.Modules Modules { get; private set; }
        public Output.Outputs Outputs { get; private set; }
        public Project Project { get; private set; }
        public RandomGenerator RandomGenerator { get; private set; }
        public ScheduledEvents ScheduledEvents { get; private set; }

        public Model(Project projectFile, Landscape landscape)
        {
            this.isDisposed = false;
            this.ruParallel = null;

            this.Project = projectFile;
            this.Landscape = landscape;

            this.Files = new FileLocations();
            this.ModelSettings = new ModelSettings();
            this.Outputs = new Output.Outputs();
            this.RandomGenerator = new RandomGenerator();

            this.Management = null;
            this.ScheduledEvents = null;

            // log level
            // TODO: use System.Diagnostics.Tracing.EventLevel
            string logLevelAsString = this.Project.System.Settings.LogLevel.ToLowerInvariant();
            int logLevel = logLevelAsString switch
            {
                "debug" => 0,
                "info" => 1,
                "warning" => 2,
                "error" => 3,
                _ => throw new NotSupportedException("Unhandled log level '" + logLevelAsString + "'.")
            };
            this.Files.SetLogLevel(logLevel);

            // random seed: if stored value is <> 0, use this as the random seed (and produce hence always an equal sequence of random numbers)
            Nullable<int> seed = this.Project.System.Settings.RandomSeed;
            this.RandomGenerator.Setup(RandomGenerator.RandomGeneratorType.MersenneTwister, seed); // use the MersenneTwister as default

            // setup of modules
            this.Modules = new Plugin.Modules();

            // setup the helper that does the multithreading
            // list of "valid" resource units
            List<ResourceUnit> validRUs = new List<ResourceUnit>();
            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                if (ru.EnvironmentID != -1)
                {
                    validRUs.Add(ru);
                }
            }
            this.ruParallel = new MaybeParallel<ResourceUnit>(validRUs)
            {
                IsMultithreaded = this.Project.System.Settings.Multithreading
            };
            // Debug.WriteLine("Multithreading enabled: " + IsMultithreaded + ", thread count: " + System.Environment.ProcessorCount);

            // setup of external modules
            this.Modules.SetupDisturbances();
            if (this.Modules.HasSetupResourceUnits())
            {
                for (int ruIndex = 0; ruIndex < this.Landscape.ResourceUnitGrid.Count; ++ruIndex)
                {
                    ResourceUnit ru = this.Landscape.ResourceUnitGrid[ruIndex];
                    if (ru != null)
                    {
                        RectangleF ruPosition = this.Landscape.ResourceUnitGrid.GetCellExtent(this.Landscape.ResourceUnitGrid.CellIndexOf(ru));
                        this.Landscape.Environment.SetPosition(this.Project, this.Landscape, ruPosition.Center()); // if environment is 'disabled' default values from the project file are used.
                        this.Modules.SetupResourceUnit(ru);
                    }
                }
            }

            // (3) additional issues
            // (3.2) setup of regeneration
            if (this.ModelSettings.RegenerationEnabled)
            {
                foreach (TreeSpeciesSet speciesSet in this.Landscape.Environment.SpeciesSetsByTableName.Values)
                {
                    speciesSet.SetupSeedDispersal(this);
                }
            }

            if (this.Project.Model.Management.Enabled)
            {
                this.Management = new Management();
                // string mgmtFile = xml.GetString("model.management.file");
                // string path = this.GlobalSettings.Path(mgmtFile, "script");
            }

            // time series data
            if (this.Project.Model.World.TimeEventsEnabled)
            {
                this.ScheduledEvents = new ScheduledEvents();
                string timeEventsFileName = this.Project.Model.World.TimeEventsFile;
                if (String.IsNullOrEmpty(timeEventsFileName))
                {
                    throw new XmlException("/project/model/world/timeEventsFile not found");
                }
                this.ScheduledEvents.LoadFromFile(this.Project, this.Project.GetFilePath(ProjectDirectory.Script, timeEventsFileName));
            }

            // TODO: is this necessary?
            this.CurrentYear = 1;
        }

        /// beforeRun performs several steps before the models starts running.
        /// inter alia: * setup of the stands
        ///             * setup of the climates
        public void Setup() // initializations
        {
            // initialize stands
            // TODO: consolidate this into Landscape?
            StandReader standReader = new StandReader();
            standReader.Setup(this.Project, this.Files, this.Landscape, this.RandomGenerator);
            standReader.SetupSaplings(this);

            this.ApplyAndReadLightPattern(); // TODO: is this needed?

            // calculate initial stand statistics
            this.CalculateStockedArea();
            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                ru.AddTreeAgingForAllTrees();
                ru.CreateStandStatistics(this);
            }

            // setup outputs
            this.Outputs.Setup(this);

            // outputs to create with inital state (without any growth) are called here:
            this.CurrentYear = 0; // set clock to "0" (for outputs with initial state)
            this.Outputs.LogYear(this); // log initial state
            this.CurrentYear = 1; // set to first year
        }

        /** Main model runner.
          The sequence of actions is as follows:
          (1) Load the climate of the new year
          (2) Reset statistics for resource unit as well as for dead/managed trees
          (3) Invoke Management.
          (4) *after* that, calculate Light patterns
          (5) 3PG on stand level, tree growth. Clear stand-statistcs before they are filled by single-tree-growth. calculate water cycle (with LAIs before management)
          (6) execute Regeneration
          (7) invoke disturbance modules
          (8) calculate carbon cycle
          (9) calculate statistics for the year
          (10) write database outputs
          */
        public void RunYear() // run a single year
        {
            //using DebugTimer t = this.DebugTimers.Create("Model.RunYear()");
            //this.GlobalSettings.SystemStatistics.Reset();
            // initalization at start of year for external modules
            this.Modules.OnStartYear();

            // execute scheduled events for the current year
            if (this.ScheduledEvents != null)
            {
                this.ScheduledEvents.RunYear(this);
            }
            // load the next year of the climate database
            foreach (World.Climate climate in this.Landscape.Environment.ClimatesByName.Values)
            {
                climate.OnStartYear(this);
            }

            // reset statistics
            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                ru.OnStartYear();
            }

            foreach (TreeSpeciesSet speciesSet in this.Landscape.Environment.SpeciesSetsByTableName.Values)
            {
                speciesSet.OnStartYear(this);
            }
            // management classic
            if (this.Management != null)
            {
                //using DebugTimer t2 = this.DebugTimers.Create("Management.Run()");
                this.Management.RunYear();
                //this.GlobalSettings.SystemStatistics.ManagementTime += t.Elapsed();
            }

            // if trees are dead/removed because of management, the tree lists
            // need to be cleaned (and the statistics need to be recreated)
            this.RemoveDeadTreesAndRecalculateStandStatistics(true); // recalculate statistics (LAIs per species needed later in production)

            // process a cycle of individual growth
            // create light influence patterns and readout light state of individual trees
            this.ApplyAndReadLightPattern();

            /** Main function for the growth of stands and trees.
               This includes several steps.
               (1) calculate the stocked area (i.e. count pixels in height grid)
               (2) 3PG production (including response calculation, water cycle)
               (3) single tree growth (including mortality)
               (4) cleanup of tree lists (remove dead trees)
              */
            // let the trees grow (growth on stand-level, tree-level, mortality)
            this.CalculateStockedArea();

            this.ruParallel.ForEach((ResourceUnit ru) =>
            {
                // 3-PG production of biomass
                ru.CalculateWaterAndBiomassGrowthForYear(this);

                ru.BeforeTreeGrowth(); // reset statistics
                // calculate light responses
                // responses are based on *modified* values for LightResourceIndex
                foreach (Trees treesOfSpecies in ru.TreesBySpeciesID.Values)
                {
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        treesOfSpecies.CalculateLightResponse(treeIndex);
                    }
                }

                ru.CalculateInterceptedArea();

                foreach (Trees treesOfSpecies in ru.TreesBySpeciesID.Values)
                {
                    treesOfSpecies.CalculateAnnualGrowth(this); // actual growth of individual trees
                }

                ru.RemoveDeadTrees();
                ru.AfterTreeGrowth();
            });

            this.Landscape.GrassCover.UpdateCoverage(this.Landscape, this.RandomGenerator); // evaluate the grass / herb cover (and its effect on regeneration)

            // regeneration
            if (this.ModelSettings.RegenerationEnabled)
            {
                // seed dispersal
                //using DebugTimer tseed = this.DebugTimers.Create("Model.RunYear(seed dispersal, establishment, sapling growth");
                foreach (TreeSpeciesSet speciesSet in this.Landscape.Environment.SpeciesSetsByTableName.Values)
                {
                    MaybeParallel<TreeSpecies> speciesParallel = new MaybeParallel<TreeSpecies>(speciesSet.ActiveSpecies); // initialize a thread runner object with all active species
                    speciesParallel.ForEach((TreeSpecies species) =>
                    {
                        species.SeedDispersal.DisperseSeeds(this);
                    });
                }
                this.ruParallel.ForEach((ResourceUnit ru) =>
                {
                    this.Landscape.Saplings.EstablishSaplings(this, ru);
                    this.Landscape.Saplings.GrowSaplings(this, ru);
                });
            }

            // external modules/disturbances
            this.Modules.RunYear();
            // cleanup of tree lists if external modules removed trees.
            this.RemoveDeadTreesAndRecalculateStandStatistics(recalculateSpecies: false); // do not recalculate statistics - this is done in ResourceUnit.YearEnd()

            // calculate soil / snag dynamics
            if (this.ModelSettings.CarbonCycleEnabled)
            {
                //using DebugTimer ccycle = this.DebugTimers.Create("Model.CarbonCycle90");
                this.ruParallel.ForEach((ResourceUnit ru) =>
                {
                    // (1) do calculations on snag dynamics for the resource unit
                    // (2) do the soil carbon and nitrogen dynamics calculations (ICBM/2N)
                    ru.CalculateCarbonCycle();
                });
                //this.GlobalSettings.SystemStatistics.CarbonCycleTime += ccycle.Elapsed();
            }

            //using DebugTimer toutput = this.DebugTimers.Create("Model.RunYear(outputs)");
            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                // calculate statistics
                ru.OnEndYear();
            }
            // create outputs
            this.Outputs.LogYear(this);

            //this.GlobalSettings.SystemStatistics.WriteOutputTime += toutput.Elapsed();
            //this.GlobalSettings.SystemStatistics.TotalYearTime += t.Elapsed();
            // this.GlobalSettings.SystemStatistics.AddToDebugList();

            ++this.CurrentYear;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed == false)
            {
                if (disposing)
                {
                    this.Outputs.Dispose();
                }
                this.isDisposed = true;
            }
        }

        public void ApplyAndReadLightPattern()
        {
            // intialize grids...
            this.InitializeLightGrid();

            // initialize height grid with a value of 4m. This is the height of the regeneration layer
            for (int heightIndex = 0; heightIndex < this.Landscape.HeightGrid.Count; ++heightIndex)
            {
                this.Landscape.HeightGrid[heightIndex].ResetTreeCount(); // set count = 0, but do not touch the flags
                this.Landscape.HeightGrid[heightIndex].Height = Constant.RegenerationLayerHeight;
            }

            this.ruParallel.ForEach((ResourceUnit ru) =>
            {
                foreach (Trees treesOfSpecies in ru.TreesBySpeciesID.Values)
                {
                    Action<int> calculateDominantHeightField = treesOfSpecies.CalculateDominantHeightField;
                    Action<int> applyLightIntensityPattern = treesOfSpecies.ApplyLightIntensityPattern;
                    Action<int> readLightInfluenceField = treesOfSpecies.ReadLightInfluenceField;
                    if (this.Project.Model.Parameter.Torus)
                    {
                        calculateDominantHeightField = treesOfSpecies.CalculateDominantHeightFieldTorus;
                        applyLightIntensityPattern = treesOfSpecies.ApplyLightIntensityPatternTorus;
                        readLightInfluenceField = treesOfSpecies.ReadLightInfluenceFieldTorus;
                    }

                    // apply light pattern
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        calculateDominantHeightField.Invoke(treeIndex);
                    }
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        applyLightIntensityPattern.Invoke(treeIndex);
                    }

                    // read pattern: LIP value calculation
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        readLightInfluenceField.Invoke(treeIndex); // multiplicative approach
                    }
                }
            });
        }

        /// clean the tree data structures (remove harvested trees) - call after management operations.
        public void RemoveDeadTreesAndRecalculateStandStatistics(bool recalculateSpecies)
        {
            foreach (ResourceUnit ru in this.Landscape.ResourceUnits)
            {
                if (ru.HasDeadTrees)
                {
                    ru.RemoveDeadTrees();
                    ru.RecreateStandStatistics(recalculateSpecies);
                }
            }
        }

        /** calculate for each resource unit the fraction of area which is stocked.
          This is done by checking the pixels of the global height grid.
          */
        private void CalculateStockedArea() // calculate area stocked with trees for each RU
        {
            // iterate over the whole heightgrid and count pixels for each resource unit
            for (int heightIndex = 0; heightIndex < this.Landscape.HeightGrid.Count; ++heightIndex)
            {
                PointF centerPoint = this.Landscape.HeightGrid.GetCellCenterPosition(heightIndex);
                if (this.Landscape.ResourceUnitGrid.Contains(centerPoint))
                {
                    ResourceUnit ru = this.Landscape.ResourceUnitGrid[centerPoint];
                    if (ru != null)
                    {
                        ru.CountHeightCell(this.Landscape.HeightGrid[heightIndex].TreeCount > 0);
                    }
                }
            }
        }

        private void InitializeLightGrid() // initialize the LIF grid
        {
            // fill the whole grid with a value of 1.0
            this.Landscape.LightGrid.Fill(1.0F);

            // apply special values for grid cells border regions where out-of-area cells
            // radiate into the main LIF grid.
            int lightOffset = Constant.LightCellsPerHeightSize / 2; // for 5 px per height grid cell, the offset is 2
            int maxRadiationDistanceInHeightCells = 7;
            float stepWidth = 1.0F / (float)maxRadiationDistanceInHeightCells;
            int borderHeightCellCount = 0;
            for (int index = 0; index < this.Landscape.HeightGrid.Count; ++index)
            {
                HeightCell heightCell = this.Landscape.HeightGrid[index];
                if (heightCell.IsRadiating())
                {
                    Point heightCellIndex = this.Landscape.HeightGrid.CellIndexOf(heightCell);
                    int minLightX = heightCellIndex.X * Constant.LightCellsPerHeightSize - maxRadiationDistanceInHeightCells + lightOffset;
                    int maxLightX = minLightX + 2 * maxRadiationDistanceInHeightCells + 1;
                    int centerLightX = minLightX + maxRadiationDistanceInHeightCells;
                    int minLightY = heightCellIndex.Y * Constant.LightCellsPerHeightSize - maxRadiationDistanceInHeightCells + lightOffset;
                    int maxLightY = minLightY + 2 * maxRadiationDistanceInHeightCells + 1;
                    int centerLightY = minLightY + maxRadiationDistanceInHeightCells;
                    for (int lightY = minLightY; lightY <= maxLightY; ++lightY)
                    {
                        for (int lightX = minLightX; lightX <= maxLightX; ++lightX)
                        {
                            if (!this.Landscape.LightGrid.Contains(lightX, lightY) || !this.Landscape.HeightGrid[lightX, lightY, Constant.LightCellsPerHeightSize].IsOnLandscape())
                            {
                                continue;
                            }
                            float candidateLightValue = MathF.Max(MathF.Abs(lightX - centerLightX), MathF.Abs(lightY - centerLightY)) * stepWidth;
                            float currentLightValue = this.Landscape.LightGrid[lightX, lightY];
                            if (candidateLightValue >= 0.0F && currentLightValue > candidateLightValue)
                            {
                                this.Landscape.LightGrid[lightX, lightY] = candidateLightValue;
                            }
                        }
                    }
                    ++borderHeightCellCount;
                }
            }

            if (this.Files.LogDebug())
            {
                Debug.WriteLine("InitializeGrid(): " + borderHeightCellCount + " radiating height cells.");
            }
        }

        //public ResourceUnit GetResourceUnit(int index)  // get resource unit by index
        //{
        //    return (index >= 0 && index < ResourceUnits.Count) ? ResourceUnits[index] : null;
        //}
    }
}
