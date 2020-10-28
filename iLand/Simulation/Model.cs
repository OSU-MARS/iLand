using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Tools;
using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;

namespace iLand.Simulation
{
    public class Model : IDisposable
    {
        private bool isDisposed;

        public ThreadRunner ThreadRunner { get; private set; }
        public double TotalStockableHectares { get; private set; } // total stockable area of the landscape (ha)
        public RectangleF WorldExtentUnbuffered { get; private set; } // extent of the model (without buffer)

        //public DebugTimerCollection DebugTimers { get; private set; }
        public DEM Dem { get; private set; }
        public Input.EnvironmentReader Environment { get; private set; }
        public GrassCover GrassCover { get; private set; }
        public FileLocations Files { get; private set; }
        public Management Management { get; private set; }
        public ModelSettings ModelSettings { get; private set; }
        public Plugin.Modules Modules { get; private set; }
        public Output.Outputs Outputs { get; private set; }
        public Project Project { get; private set; }
        public RandomGenerator RandomGenerator { get; private set; }
        public List<ResourceUnit> ResourceUnits { get; private set; }
        public Saplings Saplings { get; private set; }
        public ScheduledEvents ScheduledEvents { get; private set; }

        public bool IsSetup { get; private set; } // return true if the model world is correctly setup.

        // global grids
        public Grid<float> LightGrid { get; private set; } // this is the global 'LIF'-grid (light patterns) (currently 2x2m)
        public Grid<HeightCell> HeightGrid { get; private set; } // stores maximum heights of trees and some flags (currently 10x10m)
        public MapGrid StandGrid { get; private set; } // retrieve the spatial grid that defines the stands (10m resolution)
        public Grid<ResourceUnit> ResourceUnitGrid { get; private set; }

        public Model()
        {
            //this.DebugTimers = new DebugTimerCollection();
            this.Files = new FileLocations();
            this.ModelSettings = new ModelSettings();
            this.Outputs = new Output.Outputs();
            this.RandomGenerator = new RandomGenerator();
            this.ResourceUnits = new List<ResourceUnit>();
            this.ResourceUnitGrid = new Grid<ResourceUnit>();
            this.ThreadRunner = new ThreadRunner();

            this.IsSetup = false;
            this.LightGrid = null;
            this.HeightGrid = null;
            this.Management = null;
            this.Environment = null;
            this.ScheduledEvents = null;
            this.StandGrid = null;
            this.Modules = null;
            this.Dem = null;
            this.GrassCover = null;
            this.Saplings = null;
        }

        // start/stop/run
        //public void AfterStop() // finish and cleanup
        //{
        //    // do some cleanup
        //    // no op in C++ iLand sources
        //}

        /// beforeRun performs several steps before the models starts running.
        /// inter alia: * setup of the stands
        ///             * setup of the climates
        public void BeforeRun() // initializations
        {
            // setup outputs
            this.Outputs.Setup(this);
            //this.GlobalSettings.ClearDebugLists();

            // initialize stands
            StandReader loader = new StandReader(this);
            {
                //using DebugTimer loadTrees = this.DebugTimers.Create("StandLoader.ProcessInit()");
                loader.Setup(this);
            }

            {
                if (this.Files.LogDebug())
                {
                    Debug.WriteLine("attempting to calculate initial stand statistics (incl. apply and read pattern)...");
                }
                //using DebugTimer loadinit = this.DebugTimers.Create("Model.BeforeRun(light + height grids and statistics)");
                // debugCheckAllTrees(); // introduced for debugging session (2012-04-06)
                this.ApplyAndReadLightPattern();
                loader.SetupSaplings(this); // e.g. initialization of saplings

                // calculate initial stand statistics
                this.CalculateStockedArea();
                foreach (ResourceUnit ru in this.ResourceUnits)
                {
                    ru.AddTreeAgingForAllTrees(this);
                    ru.CreateStandStatistics(this);
                }
            }

            // outputs to create with inital state (without any growth) are called here:
            this.ModelSettings.CurrentYear = 0; // set clock to "0" (for outputs with initial state)
            this.Outputs.LogYear(this); // log initial state
            this.ModelSettings.CurrentYear = 1; // set to first year
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
            foreach (World.Climate climate in this.Environment.ClimatesByName.Values)
            {
                climate.OnStartYear(this);
            }

            // reset statistics
            foreach (ResourceUnit ru in this.ResourceUnits)
            {
                ru.OnStartYear();
            }

            foreach (TreeSpeciesSet speciesSet in this.Environment.SpeciesSetsByTableName.Values)
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
            this.ApplyAndReadLightPattern(); // create light influence patterns and readout light state of individual trees
            this.GrowTrees(); // let the trees grow (growth on stand-level, tree-level, mortality)
            this.GrassCover.UpdateCoverage(this); // evaluate the grass / herb cover (and its effect on regeneration)

            // regeneration
            if (this.ModelSettings.RegenerationEnabled)
            {
                // seed dispersal
                //using DebugTimer tseed = this.DebugTimers.Create("Model.RunYear(seed dispersal, establishment, sapling growth");
                foreach (TreeSpeciesSet speciesSet in this.Environment.SpeciesSetsByTableName.Values)
                {
                    speciesSet.DisperseSeedsForYear(this); // parallel execution for each species set
                }
                //this.GlobalSettings.SystemStatistics.SeedDistributionTime += tseed.Elapsed();
                // establishment
                {
                    //using DebugTimer t2 = this.DebugTimers.Create("Model.SaplingEstablishment()");
                    this.ExecutePerResourceUnit((ResourceUnit ru) =>
                    {
                        this.Saplings.EstablishSaplings(this, ru);
                    },
                    forceSingleThreaded: false);
                    //this.GlobalSettings.SystemStatistics.EstablishmentTime += t.Elapsed();
                }
                {
                    //using DebugTimer t3 = this.DebugTimers.Create("Model.SaplingGrowth()");
                    this.ExecutePerResourceUnit((ResourceUnit ru) =>
                    {
                        this.Saplings.GrowSaplings(this, ru);
                    }, 
                    forceSingleThreaded: false);
                    //this.GlobalSettings.SystemStatistics.SaplingTime += t.Elapsed();
                }

                // Establishment.debugInfo(); // debug test
            }

            // external modules/disturbances
            this.Modules.RunYear();
            // cleanup of tree lists if external modules removed trees.
            this.RemoveDeadTreesAndRecalculateStandStatistics(false); // do not recalculate statistics - this is done in ResourceUnit.YearEnd()

            // calculate soil / snag dynamics
            if (this.ModelSettings.CarbonCycleEnabled)
            {
                //using DebugTimer ccycle = this.DebugTimers.Create("Model.CarbonCycle90");
                this.ExecutePerResourceUnit((ResourceUnit ru) =>
                {
                    // (1) do calculations on snag dynamics for the resource unit
                    // (2) do the soil carbon and nitrogen dynamics calculations (ICBM/2N)
                    ru.CalculateCarbonCycle(this);
                }, 
                forceSingleThreaded: false);
                //this.GlobalSettings.SystemStatistics.CarbonCycleTime += ccycle.Elapsed();
            }

            //using DebugTimer toutput = this.DebugTimers.Create("Model.RunYear(outputs)");
            foreach (ResourceUnit ru in this.ResourceUnits)
            {
                // calculate statistics
                ru.OnEndYear(this);
            }
            // create outputs
            this.Outputs.LogYear(this);

            //this.GlobalSettings.SystemStatistics.WriteOutputTime += toutput.Elapsed();
            //this.GlobalSettings.SystemStatistics.TotalYearTime += t.Elapsed();
            // this.GlobalSettings.SystemStatistics.AddToDebugList();

            ++this.ModelSettings.CurrentYear;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.isDisposed)
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
            //using DebugTimer t = this.DebugTimers.Create("Model.ApplyPattern()");
            // intialize grids...
            this.InitializeLightGrid();

            // initialize height grid with a value of 4m. This is the height of the regeneration layer
            for (int heightIndex = 0; heightIndex < this.HeightGrid.Count; ++heightIndex)
            {
                this.HeightGrid[heightIndex].ResetTreeCount(); // set count = 0, but do not touch the flags
                this.HeightGrid[heightIndex].Height = Constant.RegenerationLayerHeight;
            }

            this.ThreadRunner.Run(this.CalculateHeightFieldAndLightIntensityPattern);
            //this.GlobalSettings.SystemStatistics.ApplyPatternTime += t.Elapsed();

            //using DebugTimer t = this.DebugTimers.Create("Model.ReadPattern()");
            this.ThreadRunner.Run(this.ReadPattern);
            //this.GlobalSettings.SystemStatistics.ReadPatternTime += t.Elapsed();
        }

        /** Setup of the simulation.
          This really creates the simulation environment and does the setup of various aspects.
          */
        public void LoadProject(string projectFilePath) // setup and load a project
        {
            //using DebugTimer dt = this.DebugTimers.Create("Model.LoadProject()");
            // this.GlobalSettings.PrintDirectories();
            this.Project = Project.Load(projectFilePath);
            this.Files.SetupDirectories(this.Project.System.Path, Path.GetFullPath(projectFilePath));

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

            this.ModelSettings.LoadModelSettings(this);
            if (this.Files.LogDebug())
            {
                this.ModelSettings.Print();
            }
            // random seed: if stored value is <> 0, use this as the random seed (and produce hence always an equal sequence of random numbers)
            int seed = this.Project.System.Settings.RandomSeed;
            this.RandomGenerator.Setup(RandomGenerator.RandomGeneratorType.MersenneTwister, seed); // use the MersenneTwister as default

            // setup of modules
            this.Modules = new Plugin.Modules();

            this.SetupSpace();
            if (this.ResourceUnits.Count == 0)
            {
                throw new NotSupportedException("Setup of Model: no resource units present!");
            }

            // (3) additional issues
            // (3.2) setup of regeneration
            if (this.ModelSettings.RegenerationEnabled)
            {
                foreach (TreeSpeciesSet speciesSet in this.Environment.SpeciesSetsByTableName.Values)
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
        }

        /// clean the tree data structures (remove harvested trees) - call after management operations.
        public void RemoveDeadTreesAndRecalculateStandStatistics(bool recalculateSpecies)
        {
            foreach (ResourceUnit ru in this.ResourceUnits)
            {
                if (ru.HasDeadTrees)
                {
                    ru.RemoveDeadTrees();
                    ru.RecreateStandStatistics(recalculateSpecies);
                }
            }
        }

        /// execute a function for each resource unit using multiple threads. "funcptr" is a ptr to a simple function
        private void ExecutePerResourceUnit(Action<ResourceUnit> funcptr, bool forceSingleThreaded = false)
        {
            this.ThreadRunner.Run(funcptr, forceSingleThreaded);
        }

        private void SetupSpace() // setup the "world"(spatial grids, ...), create ressource units
        {
            float lightCellSize = this.Project.Model.World.CellSize;
            float worldWidth = this.Project.Model.World.Width;
            float worldHeight = this.Project.Model.World.Height;
            float worldBuffer = this.Project.Model.World.Buffer;
            this.WorldExtentUnbuffered = new RectangleF(0.0F, 0.0F, worldWidth, worldHeight);

            Debug.WriteLine(String.Format("Setup of the world: {0}x{1} m with {2} m light cell size and {3} m buffer", worldWidth, worldHeight, lightCellSize, worldBuffer));

            RectangleF worldExtentBuffered = new RectangleF(-worldBuffer, -worldBuffer, worldWidth + 2 * worldBuffer, worldHeight + 2 * worldBuffer);
            Debug.WriteLine("Setup grid rectangle: " + worldExtentBuffered);

            this.LightGrid = new Grid<float>(worldExtentBuffered, lightCellSize);
            this.LightGrid.Fill(1.0F);
            this.HeightGrid = new Grid<HeightCell>(worldExtentBuffered, Constant.LightCellsPerHeightSize * lightCellSize);
            for (int index = 0; index < this.HeightGrid.Count; ++index)
            {
                this.HeightGrid[index] = new HeightCell();
            }

            // load environment (multiple climates, speciesSets, ...
            this.Environment = new Input.EnvironmentReader();

            // setup the spatial location of the project area
            if (this.Project.Model.World.Location != null)
            {
                // setup of spatial location
                double worldOriginX = this.Project.Model.World.Location.X;
                double worldOriginY = this.Project.Model.World.Location.Y;
                double worldOriginZ = this.Project.Model.World.Location.Z;
                double worldRotation = this.Project.Model.World.Location.Rotation;
                this.Environment.GisGrid.SetupTransformation(worldOriginX, worldOriginY, worldOriginZ, worldRotation);
                // Debug.WriteLine("Setup of spatial location: " + worldOriginX + "," + worldOriginY + "," + worldOriginZ + " rotation " + worldRotation);
            }
            else
            {
                this.Environment.GisGrid.SetupTransformation(0.0, 0.0, 0.0, 0.0);
            }

            if (this.Project.Model.World.EnvironmentEnabled)
            {
                bool isGridEnvironment = String.Equals(this.Project.Model.World.EnvironmentMode, "grid", StringComparison.Ordinal);
                if (isGridEnvironment)
                {
                    string gridFileName = this.Project.Model.World.EnvironmentGridFile;
                    if (String.IsNullOrEmpty(gridFileName))
                    {
                        throw new XmlException("/project/model/world/environmentGrid not found.");
                    }
                    string gridFilePath = this.Files.GetPath(gridFileName);
                    if (File.Exists(gridFilePath))
                    {
                        this.Environment.SetGridMode(gridFilePath);
                    }
                    else
                    {
                        throw new NotSupportedException(String.Format("File '{0}' specified in key 'environmentGrid' does not exist ('environmentMode' is 'grid').", gridFilePath));
                    }
                }

                string environmentFileName = this.Project.Model.World.EnvironmentFile;
                if (String.IsNullOrEmpty(environmentFileName))
                {
                    throw new XmlException("/project/model/world/environmentFile not found.");
                }
                string environmentFilePath = this.Files.GetPath(environmentFileName);
                if (this.Environment.LoadFromProjectAndEnvironmentFile(this, environmentFilePath) == false)
                {
                    return; // TODO: why is this here?
                }
            }
            else
            {
                throw new NotSupportedException("Environment should create default species set and climate from project file settings.");
                // create default species set and climate
                //SpeciesSet speciesSet = new SpeciesSet();
                //mSpeciesSets.Add(speciesSet);
                //speciesSet.Setup(this);
                //Climate c = new Climate();
                //Climates.Add(c);
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
                this.ScheduledEvents.LoadFromFile(this.Files, this.Files.GetPath(timeEventsFileName, "script"));
            }

            // simple case: create resource units in a regular grid.
            if (this.Project.Model.World.ResourceUnitsAsGrid == false)
            {
                throw new NotImplementedException("For now, /project/world/resourceUnitsAsGrid must be set to true.");
            }

            this.ResourceUnitGrid.Setup(new RectangleF(0.0F, 0.0F, worldWidth, worldHeight), 100.0F); // Grid, that holds positions of resource units
            this.ResourceUnitGrid.FillDefault();

            bool hasStandGrid = false;
            if (this.Project.Model.World.StandGrid.Enabled)
            {
                string fileName = this.Project.Model.World.StandGrid.FileName;
                this.StandGrid = new MapGrid(this, fileName); // create stand grid index later
                if (this.StandGrid.IsValid() == false)
                {
                    throw new NotSupportedException();
                }

                for (int standIndex = 0; standIndex < StandGrid.Grid.Count; standIndex++)
                {
                    int standID = this.StandGrid.Grid[standIndex];
                    this.HeightGrid[standIndex].SetInWorld(standID > -1);
                }
                hasStandGrid = true;
            }
            else
            {
                if (this.ModelSettings.IsTorus == false)
                {
                    // in the case we have no stand grid but only a large rectangle (without the torus option)
                    // we assume a forest outside
                    for (int heightIndex = 0; heightIndex < this.HeightGrid.Count; ++heightIndex)
                    {
                        PointF heightPosition = this.HeightGrid.GetCellCenterPosition(heightIndex);
                        if (heightPosition.X < 0.0F || heightPosition.X > worldWidth || heightPosition.Y < 0.0F || heightPosition.Y > worldHeight)
                        {
                            this.HeightGrid[heightIndex].SetInWorld(false);
                        }
                    }
                }
            }

            if (this.StandGrid == null || this.StandGrid.IsValid() == false)
            {
                for (int ruGridIndex = 0; ruGridIndex < this.ResourceUnitGrid.Count; ++ruGridIndex)
                {
                    // create resource units for valid positions only
                    RectangleF ruExtent = this.ResourceUnitGrid.GetCellExtent(ResourceUnitGrid.GetCellPosition(ruGridIndex));
                    this.Environment.SetPosition(ruExtent.Center(), this); // if environment is 'disabled' default values from the project file are used.
                    ResourceUnit newRU = new ResourceUnit(ruGridIndex)
                    {
                        BoundingBox = ruExtent,
                        Climate = this.Environment.CurrentClimate,
                        EnvironmentID = this.Environment.CurrentResourceUnitID, // set id of resource unit in grid mode
                        TopLeftLightPosition = this.LightGrid.GetCellIndex(ruExtent.TopLeft())
                    };
                    newRU.SetSpeciesSet(this.Environment.CurrentSpeciesSet);
                    newRU.Setup(this);
                    this.ResourceUnits.Add(newRU);
                    this.ResourceUnitGrid[ruGridIndex] = newRU; // save in the RUmap grid
                }
            }
            //if (Environment != null)
            //{
            //    StringBuilder climateFiles = new StringBuilder();
            //    for (int i = 0, c = 0; i < Climates.Count; ++i)
            //    {
            //        climateFiles.Append(Climates[i].Name + ", ");
            //        if (++c > 5)
            //        {
            //            climateFiles.Append("...");
            //            break;
            //        }
            //    }
            //    Debug.WriteLine("Setup of climates: #loaded: " + Climates.Count + " tables: " + climateFiles);
            //}
            // Debug.WriteLine("Setup of " + this.Environment.ClimatesByName.Count + " climate(s) performed.");

            if (this.StandGrid != null && this.StandGrid.IsValid())
            {
                this.StandGrid.CreateIndex(this);
            }
            // now store the pointers in the grid.
            // Important: This has to be done after the mRU-QList is complete - otherwise pointers would
            // point to invalid memory when QList's memory is reorganized (expanding)
            //        ru_index = 0;
            //        for (p=mRUmap.begin();p!=mRUmap.end(); ++p) {
            //            *p = mRU.value(ru_index++);
            //        }
            Debug.WriteLine("Created grid of " + ResourceUnits.Count + " resource units in " + ResourceUnitGrid.Count + " map cells.");
            this.CalculateStockableArea();

            // setup of the project area mask
            if ((hasStandGrid == false) && this.Project.Model.World.AreaMask.Enabled && (this.Project.Model.World.AreaMask.ImageFile != null)) // TODO: String.IsNullOrEmpty(ImageFile)?
            {
                // to be extended!!! e.g. to load ESRI-style text files....
                // setup a grid with the same size as the height grid...
                Grid<float> worldMask = new Grid<float>(this.HeightGrid.CellsX, this.HeightGrid.CellsY, this.HeightGrid.CellSize);
                string fileName = this.Files.GetPath(this.Project.Model.World.AreaMask.ImageFile);
                Grid.LoadGridFromImage(fileName, worldMask); // fetch from image
                for (int index = 0; index < worldMask.Count; ++index)
                {
                    this.HeightGrid[index].SetInWorld(worldMask[index] > 0.99);
                }
                Debug.WriteLine("loaded project area mask from" + fileName);
            }

            // setup of the digital elevation map (if present)
            string demFileName = this.Project.Model.World.DemFile;
            if (String.IsNullOrEmpty(demFileName) == false)
            {
                this.Dem = new DEM(this.Files.GetPath(demFileName), this);
            }

            // setup of saplings
            if (this.Saplings != null)
            {
                this.Saplings = null;
            }
            if (this.ModelSettings.RegenerationEnabled)
            {
                this.Saplings = new Saplings();
                this.Saplings.Setup(this);
            }

            // setup of the grass cover
            if (this.GrassCover == null)
            {
                this.GrassCover = new GrassCover();
            }
            this.GrassCover.Setup(this);

            // setup of external modules
            this.Modules.SetupDisturbances();
            if (this.Modules.HasSetupResourceUnits())
            {
                for (int ruIndex = 0; ruIndex < this.ResourceUnitGrid.Count; ++ruIndex)
                {
                    ResourceUnit ru = ResourceUnitGrid[ruIndex];
                    if (ru != null)
                    {
                        RectangleF ruPosition = this.ResourceUnitGrid.GetCellExtent(this.ResourceUnitGrid.CellIndexOf(ru));
                        this.Environment.SetPosition(ruPosition.Center(), this); // if environment is 'disabled' default values from the project file are used.
                        this.Modules.SetupResourceUnit(ru);
                    }
                }
            }

            // setup the helper that does the multithreading
            // list of "valid" resource units
            List<ResourceUnit> validRUs = new List<ResourceUnit>();
            foreach (ResourceUnit ru in this.ResourceUnits)
            {
                if (ru.EnvironmentID != -1)
                {
                    validRUs.Add(ru);
                }
            }
            this.ThreadRunner.Setup(validRUs);
            this.ThreadRunner.IsMultithreaded = this.Project.System.Settings.Multithreading;
            // Debug.WriteLine("Multithreading enabled: " + IsMultithreaded + ", thread count: " + System.Environment.ProcessorCount);

            this.IsSetup = true;
        }

        /** Main function for the growth of stands and trees.
           This includes several steps.
           (1) calculate the stocked area (i.e. count pixels in height grid)
           (2) 3PG production (including response calculation, water cycle)
           (3) single tree growth (including mortality)
           (4) cleanup of tree lists (remove dead trees)
          */
        private void GrowTrees() // grow - both on RU-level and tree-level
        {
            {
                //using DebugTimer t = this.DebugTimers.Create("Model.Production()");
                this.CalculateStockedArea();

                // Production of biomass (stand level, 3PG)
                this.ThreadRunner.Run((ResourceUnit ru) =>
                {
                    ru.CalculateBiomassGrowthForYear(this);
                });
            }

            //using DebugTimer t2 = this.DebugTimers.Create("Model.Grow()");
            this.ThreadRunner.Run(this.GrowTrees); // actual growth of individual trees

            foreach (ResourceUnit ru in this.ResourceUnits)
            {
                ru.RemoveDeadTrees();
                ru.AfterTreeGrowth();
                //Debug.WriteLine((b-n) + "trees died (of" + b + ").");
            }
            //this.GlobalSettings.SystemStatistics.TreeGrowthTime += t2.Elapsed();
        }

        /** calculate for each resource unit the fraction of area which is stocked.
          This is done by checking the pixels of the global height grid.
          */
        private void CalculateStockedArea() // calculate area stocked with trees for each RU
        {
            // iterate over the whole heightgrid and count pixels for each resource unit
            for (int heightIndex = 0; heightIndex < this.HeightGrid.Count; ++heightIndex)
            {
                PointF centerPoint = this.HeightGrid.GetCellCenterPosition(heightIndex);
                if (this.ResourceUnitGrid.Contains(centerPoint))
                {
                    ResourceUnit ru = this.ResourceUnitGrid[centerPoint];
                    if (ru != null)
                    {
                        ru.CountHeightCell(this.HeightGrid[heightIndex].TreeCount > 0);
                    }
                }
            }
        }

        /** calculate for each resource unit the stockable area.
          "stockability" is determined by the isValid flag of resource units which in turn
          is derived from stand grid values.
          */
        private void CalculateStockableArea() // calculate the stockable area for each RU (i.e.: with stand grid values <> -1)
        {
            this.TotalStockableHectares = 0.0;
            foreach (ResourceUnit ru in this.ResourceUnits)
            {
                //        if (ru.id()==-1) {
                //            ru.setStockableArea(0.);
                //            continue;
                //        }
                GridWindowEnumerator<HeightCell> heightRunner = new GridWindowEnumerator<HeightCell>(this.HeightGrid, ru.BoundingBox);
                int heightCellsInWorld = 0;
                int heightCellsInRU = 0;
                while (heightRunner.MoveNext())
                {
                    HeightCell current = heightRunner.Current;
                    if (current != null && current.IsInWorld())
                    {
                        ++heightCellsInWorld;
                    }
                    ++heightCellsInRU;
                }

                if (heightCellsInRU < 1)
                {
                    // TODO: check against Constant.HeightSizePerRU * Constant.HeightSizePerRU?
                    throw new NotSupportedException("No height cells found in resource unit.");
                }

                ru.StockableArea = Constant.HeightPixelArea * heightCellsInWorld; // in m2
                if (ru.Snags != null)
                {
                    ru.Snags.ScaleInitialState();
                }
                this.TotalStockableHectares += Constant.HeightPixelArea * heightCellsInWorld / Constant.RUArea; // in ha

                if (heightCellsInWorld == 0 && ru.EnvironmentID > -1)
                {
                    // invalidate this resource unit
                    // ru.ID = -1;
                    throw new NotSupportedException("Valid resource unit has no height cells in world.");
                }
                if (heightCellsInWorld > 0 && ru.EnvironmentID == -1)
                {
                    throw new NotSupportedException("Invalid resource unit " + ru.GridIndex + " (" + ru.BoundingBox + ") has height cells in world.");
                    //ru.ID = 0;
                    // test-code
                    //GridRunner<HeightGridValue> runner(*mHeightGrid, ru.boundingBox());
                    //while (runner.next()) {
                    //    Debug.WriteLine(mHeightGrid.cellCenterPoint(mHeightGrid.indexOf( runner.current() )) + ": " + runner.current().isValid());
                    //}
                }
            }

            // mark those pixels that are at the edge of a "forest-out-of-area"
            // Use GridWindowEnumerator rather than cell indexing in order to be able to access neighbors.
            GridWindowEnumerator<HeightCell> runner = new GridWindowEnumerator<HeightCell>(this.HeightGrid, this.HeightGrid.PhysicalExtent);
            HeightCell[] neighbors = new HeightCell[8];
            while (runner.MoveNext())
            {
                if (runner.Current.IsInWorld() == false)
                {
                    // if the current pixel is a "radiating" border pixel,
                    // then check the neighbors and set a flag if the pixel is a neighbor of a in-project-area pixel.
                    runner.GetNeighbors8(neighbors);
                    for (int neighborIndex = 0; neighborIndex < neighbors.Length; ++neighborIndex)
                    {
                        if (neighbors[neighborIndex] != null && neighbors[neighborIndex].IsInWorld())
                        {
                            runner.Current.SetIsRadiating();
                        }
                    }
                }
            }

            if (this.Files.LogDebug())
            {
                Debug.WriteLine("Total stockable area of the landscape is " + this.TotalStockableHectares + " ha.");
            }
        }

        private void InitializeLightGrid() // initialize the LIF grid
        {
            // fill the whole grid with a value of 1.0
            this.LightGrid.Fill(1.0F);

            // apply special values for grid cells border regions where out-of-area cells
            // radiate into the main LIF grid.
            int lightOffset = Constant.LightCellsPerHeightSize / 2; // for 5 px per height grid cell, the offset is 2
            int maxRadiationDistanceInHeightCells = 7;
            float stepWidth = 1.0F / (float)maxRadiationDistanceInHeightCells;
            int borderHeightCellCount = 0;
            for (int index = 0; index < this.HeightGrid.Count; ++index)
            {
                HeightCell heightCell = this.HeightGrid[index];
                if (heightCell.IsRadiating())
                {
                    Point heightCellIndex = this.HeightGrid.CellIndexOf(heightCell);
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
                            if (!this.LightGrid.Contains(lightX, lightY) || !this.HeightGrid[lightX, lightY, Constant.LightCellsPerHeightSize].IsInWorld())
                            {
                                continue;
                            }
                            float candidateLightValue = MathF.Max(MathF.Abs(lightX - centerLightX), MathF.Abs(lightY - centerLightY)) * stepWidth;
                            float currentLightValue = this.LightGrid[lightX, lightY];
                            if (candidateLightValue >= 0.0F && currentLightValue > candidateLightValue)
                            {
                                this.LightGrid[lightX, lightY] = candidateLightValue;
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

        /// multithreaded running function for LIP printing
        private void CalculateHeightFieldAndLightIntensityPattern(ResourceUnit ru)
        {
            foreach (Trees treesOfSpecies in ru.TreesBySpeciesID.Values)
            {
                Action<int> calculateDominantHeightField = treesOfSpecies.CalculateDominantHeightField;
                Action<int> applyLightIntensityPattern = treesOfSpecies.ApplyLightIntensityPattern;
                if (this.ModelSettings.IsTorus)
                {
                    calculateDominantHeightField = treesOfSpecies.CalculateDominantHeightFieldTorus;
                    applyLightIntensityPattern = treesOfSpecies.ApplyLightIntensityPatternTorus;
                }

                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    calculateDominantHeightField.Invoke(treeIndex);
                }
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    applyLightIntensityPattern.Invoke(treeIndex);
                }
            }
        }

        /// LIP value extraction
        private void ReadPattern(ResourceUnit ru)
        {
            foreach (Trees treesOfSpecies in ru.TreesBySpeciesID.Values)
            {
                Action<Model, int> readLightInfluenceField = treesOfSpecies.ReadLightInfluenceField;
                if (this.ModelSettings.IsTorus)
                {
                    readLightInfluenceField = treesOfSpecies.ReadLightInfluenceFieldTorus;
                }

                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    readLightInfluenceField.Invoke(this, treeIndex); // multiplicative approach
                }
            }
        }

        /// multithreaded running function for the growth of individual trees
        private void GrowTrees(ResourceUnit ru)
        {
            ru.BeforeTreeGrowth(); // reset statistics
            // calculate light responses
            // responses are based on *modified* values for LightResourceIndex
            foreach (Trees treesOfSpecies in ru.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    treesOfSpecies.CalculateLightResponse(this, treeIndex);
                }
            }

            ru.CalculateInterceptedArea();

            foreach (Trees treesOfSpecies in ru.TreesBySpeciesID.Values)
            {
                treesOfSpecies.CalculateAnnualGrowth(this); // actual growth of individual trees
            }

            //this.GlobalSettings.SystemStatistics.TreeCount += unit.Trees.Count;
        }

        public ResourceUnit GetResourceUnit(PointF ruPosition) // resource unit at given coordinates
        {
            if (this.ResourceUnitGrid.IsEmpty())
            {
                // TODO: why not just populate grid with the default resource unit?
                return this.ResourceUnits[0]; // default RU if there is only one
            }
            return this.ResourceUnitGrid[ruPosition];
        }

        //public ResourceUnit GetResourceUnit(int index)  // get resource unit by index
        //{
        //    return (index >= 0 && index < ResourceUnits.Count) ? ResourceUnits[index] : null;
        //}
    }
}
