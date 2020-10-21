using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Plugin;
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

        // access to elements
        public ThreadRunner ThreadRunner { get; private set; }
        public RectangleF WorldExtentUnbuffered { get; private set; } // extent of the model (without buffer)
        public double TotalStockableHectares { get; private set; } // total stockable area of the landscape (ha)

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
        public TimeEvents TimeEvents { get; private set; }

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
            this.TimeEvents = null;
            this.StandGrid = null;
            this.Modules = null;
            this.Dem = null;
            this.GrassCover = null;
            this.Saplings = null;
        }

        public bool IsMultithreaded() { return ThreadRunner.IsMultithreaded; } // BUGBUG

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
                loader.ProcessInit(this);
            }

            {
                if (this.Files.LogDebug())
                {
                    Debug.WriteLine("attempting to calculate initial stand statistics (incl. apply and read pattern)...");
                }
                //using DebugTimer loadinit = this.DebugTimers.Create("Model.BeforeRun(light + height grids and statistics)");
                // debugCheckAllTrees(); // introduced for debugging session (2012-04-06)
                ApplyPattern();
                ReadPattern();
                loader.ProcessAfterInit(this); // e.g. initialization of saplings

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
            Modules.YearBegin();

            // execute scheduled events for the current year
            if (TimeEvents != null)
            {
                TimeEvents.Run(this);
            }
            // load the next year of the climate database
            foreach (World.Climate climate in this.Environment.ClimatesByName.Values)
            {
                climate.NextYear(this);
            }

            // reset statistics
            foreach (ResourceUnit ru in ResourceUnits)
            {
                ru.NewYear();
            }

            foreach (SpeciesSet speciesSet in this.Environment.SpeciesSetsByTableName.Values)
            {
                speciesSet.NewYear(this);
            }
            // management classic
            if (this.Management != null)
            {
                //using DebugTimer t2 = this.DebugTimers.Create("Management.Run()");
                this.Management.Run();
                //this.GlobalSettings.SystemStatistics.ManagementTime += t.Elapsed();
            }

            // if trees are dead/removed because of management, the tree lists
            // need to be cleaned (and the statistics need to be recreated)
            CleanTreeLists(true); // recalculate statistics (LAIs per species needed later in production)

            // process a cycle of individual growth
            ApplyPattern(); // create Light Influence Patterns
            ReadPattern(); // readout light state of individual trees
            Grow(); // let the trees grow (growth on stand-level, tree-level, mortality)
            GrassCover.Execute(this); // evaluate the grass / herb cover (and its effect on regeneration)

            // regeneration
            if (ModelSettings.RegenerationEnabled)
            {
                // seed dispersal
                //using DebugTimer tseed = this.DebugTimers.Create("Model.RunYear(seed dispersal, establishment, sapling growth");
                foreach (SpeciesSet set in this.Environment.SpeciesSetsByTableName.Values)
                {
                    set.Regeneration(this); // parallel execution for each species set
                }
                //this.GlobalSettings.SystemStatistics.SeedDistributionTime += tseed.Elapsed();
                // establishment
                {
                    //using DebugTimer t2 = this.DebugTimers.Create("Model.SaplingEstablishment()");
                    ExecutePerResourceUnit(SaplingEstablishment, false /* true: force single threaded operation */);
                    //this.GlobalSettings.SystemStatistics.EstablishmentTime += t.Elapsed();
                }
                {
                    //using DebugTimer t3 = this.DebugTimers.Create("Model.SaplingGrowth()");
                    ExecutePerResourceUnit(SaplingGrowth, false /* true: force single threaded operation */);
                    //this.GlobalSettings.SystemStatistics.SaplingTime += t.Elapsed();
                }

                // Establishment.debugInfo(); // debug test
            }

            // external modules/disturbances
            Modules.Run();
            // cleanup of tree lists if external modules removed trees.
            CleanTreeLists(false); // do not recalculate statistics - this is done in ResourceUnit.YearEnd()


            // calculate soil / snag dynamics
            if (ModelSettings.CarbonCycleEnabled)
            {
                //using DebugTimer ccycle = this.DebugTimers.Create("Model.CarbonCycle90");
                ExecutePerResourceUnit(CarbonCycle, false /* true: force single threaded operation */);
                //this.GlobalSettings.SystemStatistics.CarbonCycleTime += ccycle.Elapsed();
            }

            //using DebugTimer toutput = this.DebugTimers.Create("Model.RunYear(outputs)");
            // calculate statistics
            foreach (ResourceUnit ru in ResourceUnits)
            {
                ru.YearEnd(this);
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

        public void OnlyApplyLightPattern()
        {
            ApplyPattern();
            ReadPattern();
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
            RandomGenerator.Setup(RandomGenerator.RandomGenerators.MersenneTwister, seed); // use the MersenneTwister as default

            // snag dynamics / soil model enabled? (info used during setup of world)
            ModelSettings.CarbonCycleEnabled = this.Project.Model.Settings.CarbonCycleEnabled;

            // setup of modules
            Modules = new Plugin.Modules();
            ModelSettings.RegenerationEnabled = this.Project.Model.Settings.RegenerationEnabled;

            SetupSpace();
            if (ResourceUnits.Count == 0)
            {
                throw new NotSupportedException("Setup of Model: no resource units present!");
            }

            // (3) additional issues
            // (3.2) setup of regeneration
            if (ModelSettings.RegenerationEnabled)
            {
                foreach (SpeciesSet speciesSet in this.Environment.SpeciesSetsByTableName.Values)
                {
                    speciesSet.SetupRegeneration(this);
                }
            }

            if (this.Project.Model.Management.Enabled)
            {
                Management = new Management();
                // string mgmtFile = xml.GetString("model.management.file");
                // string path = this.GlobalSettings.Path(mgmtFile, "script");
            }
        }

        /// get the value of the (10m) Height grid at the position index ix and iy (of the LIF grid)
        public HeightCell HeightGridValue(int ix, int iy)
        {
            return HeightGrid[ix / Constant.LightPerHeightSize, iy / Constant.LightPerHeightSize];
        }

        public SpeciesSet GetFirstSpeciesSet()
        {
            // BUGBUG: unsafe if more than one species set
            return this.Environment.SpeciesSetsByTableName.Values.First();
        }

        /// clean the tree data structures (remove harvested trees) - call after management operations.
        public void CleanTreeLists(bool recalculateSpecies)
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
        public void ExecutePerResourceUnit(Action<ResourceUnit> funcptr, bool forceSingleThreaded = false)
        {
            ThreadRunner.Run(funcptr, forceSingleThreaded);
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
            this.LightGrid.Initialize(1.0F);
            this.HeightGrid = new Grid<HeightCell>(worldExtentBuffered, Constant.LightPerHeightSize * lightCellSize);
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
                this.Environment.GisGrid.SetupGISTransformation(worldOriginX, worldOriginY, worldOriginZ, worldRotation);
                // Debug.WriteLine("Setup of spatial location: " + worldOriginX + "," + worldOriginY + "," + worldOriginZ + " rotation " + worldRotation);
            }
            else
            {
                this.Environment.GisGrid.SetupGISTransformation(0.0, 0.0, 0.0, 0.0);
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
                        Environment.SetGridMode(gridFilePath);
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
                if (Environment.LoadFromProjectAndEnvironmentFile(this, environmentFilePath) == false)
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
                TimeEvents = new TimeEvents();
                string timeEventsFileName = this.Project.Model.World.TimeEventsFile;
                if (String.IsNullOrEmpty(timeEventsFileName))
                {
                    throw new XmlException("/project/model/world/timeEventsFile not found");
                }
                TimeEvents.LoadFromFile(this.Files, this.Files.GetPath(timeEventsFileName, "script"));
            }

            // simple case: create resource units in a regular grid.
            if (this.Project.Model.World.ResourceUnitsAsGrid == false)
            {
                throw new NotImplementedException("For now, /project/world/resourceUnitsAsGrid must be set to true.");
            }

            ResourceUnitGrid.Setup(new RectangleF(0.0F, 0.0F, worldWidth, worldHeight), 100.0); // Grid, that holds positions of resource units
            ResourceUnitGrid.ClearDefault();

            bool hasStandGrid = false;
            if (this.Project.Model.World.StandGrid.Enabled)
            {
                string fileName = this.Project.Model.World.StandGrid.FileName;
                this.StandGrid = new MapGrid(this, fileName, false); // create stand grid index later

                if (this.StandGrid.IsValid())
                {
                    for (int standIndex = 0; standIndex < StandGrid.Grid.Count; standIndex++)
                    {
                        int standID = StandGrid.Grid[standIndex];
                        HeightGrid[standIndex].SetInWorld(standID > -1);
                        // BUGBUG: unclear why this is present in C++, appears removable
                        //if (grid_value > -1)
                        //{
                        //    mRUmap[mStandGrid.grid().cellCenterPoint(i)] = (ResourceUnit)1;
                        //}
                        if (standID < -1)
                        {
                            HeightGrid[standIndex].SetIsOutsideWorld(true);
                        }
                    }
                }
                hasStandGrid = true;
            }
            else
            {
                if (ModelSettings.IsTorus == false)
                {
                    // in the case we have no stand grid but only a large rectangle (without the torus option)
                    // we assume a forest outside
                    for (int i = 0; i < HeightGrid.Count; ++i)
                    {
                        PointF p = HeightGrid.GetCellCenterPoint(HeightGrid.IndexOf(i));
                        if (p.X < 0.0F || p.X > worldWidth || p.Y < 0.0F || p.Y > worldHeight)
                        {
                            HeightGrid[i].SetIsOutsideWorld(true);
                            HeightGrid[i].SetInWorld(false);
                        }
                    }
                }
            }

            for (int ruIndex = 0; ruIndex < ResourceUnitGrid.Count; ++ruIndex)
            {
                if (StandGrid == null || !StandGrid.IsValid())
                {
                    // create resource units for valid positions only
                    RectangleF ruExtent = ResourceUnitGrid.GetCellRect(ResourceUnitGrid.IndexOf(ruIndex));
                    Environment.SetPosition(ruExtent.Center(), this); // if environment is 'disabled' default values from the project file are used.
                    ResourceUnit newRU = new ResourceUnit(ruIndex)
                    {
                        BoundingBox = ruExtent,
                        Climate = Environment.CurrentClimate,
                        ID = Environment.CurrentResourceUnitID, // set id of resource unit in grid mode
                        TopLeftLightOffset = this.LightGrid.IndexAt(ruExtent.TopLeft())
                    };
                    newRU.SetSpeciesSet(Environment.CurrentSpeciesSet);
                    newRU.Setup(this);
                    ResourceUnits.Add(newRU);
                    ResourceUnitGrid[ruIndex] = newRU; // save in the RUmap grid
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

            if (StandGrid != null && StandGrid.IsValid())
            {
                StandGrid.CreateIndex(this);
            }
            // now store the pointers in the grid.
            // Important: This has to be done after the mRU-QList is complete - otherwise pointers would
            // point to invalid memory when QList's memory is reorganized (expanding)
            //        ru_index = 0;
            //        for (p=mRUmap.begin();p!=mRUmap.end(); ++p) {
            //            *p = mRU.value(ru_index++);
            //        }
            Debug.WriteLine("Created grid of " + ResourceUnits.Count + " resource units in " + ResourceUnitGrid.Count + " map cells.");
            CalculateStockableArea();

            // setup of the project area mask
            if ((hasStandGrid == false) && this.Project.Model.World.AreaMask.Enabled && (this.Project.Model.World.AreaMask.ImageFile != null)) // TODO: String.IsNullOrEmpty(ImageFile)?
            {
                // to be extended!!! e.g. to load ESRI-style text files....
                // setup a grid with the same size as the height grid...
                Grid<float> worldMask = new Grid<float>((int)HeightGrid.CellSize, HeightGrid.CellsX, HeightGrid.CellsY);
                string fileName = this.Files.GetPath(this.Project.Model.World.AreaMask.ImageFile);
                Grid.LoadGridFromImage(fileName, worldMask); // fetch from image
                for (int i = 0; i < worldMask.Count; i++)
                {
                    HeightGrid[i].SetInWorld(worldMask[i] > 0.99);
                }
                Debug.WriteLine("loaded project area mask from" + fileName);
            }

            // list of "valid" resource units
            List<ResourceUnit> valid_rus = new List<ResourceUnit>();
            foreach (ResourceUnit ru in ResourceUnits)
            {
                if (ru.ID != -1)
                {
                    valid_rus.Add(ru);
                }
            }

            // setup of the digital elevation map (if present)
            string demFileName = this.Project.Model.World.DemFile;
            if (String.IsNullOrEmpty(demFileName) == false)
            {
                this.Dem = new DEM(this.Files.GetPath(demFileName), this);
            }

            // setup of saplings
            if (Saplings != null)
            {
                Saplings = null;
            }
            if (ModelSettings.RegenerationEnabled)
            {
                Saplings = new Saplings();
                Saplings.Setup(this);
            }

            // setup of the grass cover
            if (GrassCover == null)
            {
                GrassCover = new GrassCover();
            }
            GrassCover.Setup(this);

            // setup of external modules
            Modules.SetupDisturbances();
            if (Modules.HasSetupResourceUnits())
            {
                for (int index = 0; index < ResourceUnitGrid.Count; ++index)
                {
                    ResourceUnit p = ResourceUnitGrid[index];
                    if (p != null)
                    {
                        RectangleF r = ResourceUnitGrid.GetCellRect(ResourceUnitGrid.IndexOf(p));
                        Environment.SetPosition(r.Center(), this); // if environment is 'disabled' default values from the project file are used.
                        Modules.SetupResourceUnit(p);
                    }
                }
            }

            // setup the helper that does the multithreading
            ThreadRunner.Setup(valid_rus);
            ThreadRunner.IsMultithreaded = this.Project.System.Settings.Multithreading;
            // Debug.WriteLine("Multithreading enabled: " + IsMultithreaded + ", thread count: " + System.Environment.ProcessorCount);

            IsSetup = true;
        }

        private void ApplyPattern() // apply LIP-patterns of all trees
        {
            //using DebugTimer t = this.DebugTimers.Create("Model.ApplyPattern()");
            // intialize grids...
            InitializeGrid();

            // initialize height grid with a value of 4m. This is the height of the regeneration layer
            for (int h = 0; h < HeightGrid.Count; ++h)
            {
                HeightGrid[h].ResetTreeCount(); // set count = 0, but do not touch the flags
                HeightGrid[h].Height = Constant.RegenerationLayerHeight;
            }

            ThreadRunner.Run(CalculateHeightFieldAndLightIntensityPattern);
            //this.GlobalSettings.SystemStatistics.ApplyPatternTime += t.Elapsed();
        }

        private void ReadPattern() // retrieve LRI for trees
        {
            //using DebugTimer t = this.DebugTimers.Create("Model.ReadPattern()");
            ThreadRunner.Run(ReadPattern);
            //this.GlobalSettings.SystemStatistics.ReadPatternTime += t.Elapsed();
        }

        /** Main function for the growth of stands and trees.
           This includes several steps.
           (1) calculate the stocked area (i.e. count pixels in height grid)
           (2) 3PG production (including response calculation, water cycle)
           (3) single tree growth (including mortality)
           (4) cleanup of tree lists (remove dead trees)
          */
        private void Grow() // grow - both on RU-level and tree-level
        {
            {
                //using DebugTimer t = this.DebugTimers.Create("Model.Production()");
                CalculateStockedArea();

                // Production of biomass (stand level, 3PG)
                ThreadRunner.Run(Production);
            }

            //using DebugTimer t2 = this.DebugTimers.Create("Model.Grow()");
            ThreadRunner.Run(Grow); // actual growth of individual trees

            foreach (ResourceUnit ru in ResourceUnits)
            {
                ru.RemoveDeadTrees();
                ru.AfterGrow();
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
            for (int heightIndex = 0; heightIndex < HeightGrid.Count; ++heightIndex)
            {
                PointF centerPoint = HeightGrid.GetCellCenterPoint(heightIndex);
                if (ResourceUnitGrid.Contains(centerPoint))
                {
                    ResourceUnit ru = ResourceUnitGrid[centerPoint];
                    if (ru != null)
                    {
                        ru.AddHeightCell(HeightGrid[heightIndex].TreeCount > 0);
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
            TotalStockableHectares = 0.0;
            foreach (ResourceUnit ru in ResourceUnits)
            {
                //        if (ru.id()==-1) {
                //            ru.setStockableArea(0.);
                //            continue;
                //        }
                GridRunner<HeightCell> heightRunner = new GridRunner<HeightCell>(HeightGrid, ru.BoundingBox);
                int heightCellsInWorld = 0;
                int heightCellsInRU = 0;
                for (heightRunner.MoveNext(); heightRunner.IsValid(); heightRunner.MoveNext())
                {
                    HeightCell current = heightRunner.Current;
                    if (current != null && current.IsInWorld())
                    {
                        ++heightCellsInWorld;
                    }
                    ++heightCellsInRU;
                }
                if (heightCellsInRU != 0)
                {
                    ru.StockableArea = Constant.HeightPixelArea * heightCellsInWorld; // in m2
                    if (ru.Snags != null)
                    {
                        ru.Snags.ScaleInitialState();
                    }
                    this.TotalStockableHectares += Constant.HeightPixelArea * heightCellsInWorld / Constant.RUArea; // in ha
                    if (heightCellsInWorld == 0 && ru.ID > -1)
                    {
                        // invalidate this resource unit
                        ru.ID = -1;
                    }
                    if (heightCellsInWorld > 0 && ru.ID == -1)
                    {
                        Debug.WriteLine("Warning: a resource unit has id=-1 but stockable area (id was set to 0)!!! ru: " + ru.BoundingBox + "with index" + ru.Index);
                        ru.ID = 0;
                        // test-code
                        //GridRunner<HeightGridValue> runner(*mHeightGrid, ru.boundingBox());
                        //while (runner.next()) {
                        //    Debug.WriteLine(mHeightGrid.cellCenterPoint(mHeightGrid.indexOf( runner.current() )) + ": " + runner.current().isValid());
                        //}
                    }
                }
                else
                {
                    throw new NotSupportedException("calculateStockableArea: resource unit without pixels!");
                }
            }

            // mark those pixels that are at the edge of a "forest-out-of-area"
            GridRunner<HeightCell> runner = new GridRunner<HeightCell>(HeightGrid, HeightGrid.PhysicalExtent);
            HeightCell[] neighbors = new HeightCell[8];
            for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
            {
                if (runner.Current.IsOutsideWorld())
                {
                    // if the current pixel is a "radiating" border pixel,
                    // then check the neighbors and set a flag if the pixel is a neighbor of a in-project-area pixel.
                    runner.Neighbors8(neighbors);
                    for (int neighborIndex = 0; neighborIndex < neighbors.Length; ++neighborIndex)
                    {
                        if (neighbors[neighborIndex] != null && neighbors[neighborIndex].IsInWorld())
                        {
                            runner.Current.SetIsRadiating();
                        }
                    }
                }
            }

            Debug.WriteLine("Total stockable area of the landscape is" + TotalStockableHectares + "ha.");
        }

        private void InitializeGrid() // initialize the LIF grid
        {
            // fill the whole grid with a value of "1."
            LightGrid.Initialize(1.0F);

            // apply special values for grid cells border regions where out-of-area cells
            // radiate into the main LIF grid.
            Point p;
            int ix_min, ix_max, iy_min, iy_max, ix_center, iy_center;
            int px_offset = Constant.LightPerHeightSize / 2; // for 5 px per height grid cell, the offset is 2
            int max_radiate_distance = 7;
            float step_width = 1.0f / (float)max_radiate_distance;
            int c_rad = 0;
            for (int index = 0; index < HeightGrid.Count; ++index)
            {
                HeightCell hgv = HeightGrid[index];
                if (hgv.IsRadiating())
                {
                    p = HeightGrid.IndexOf(hgv);
                    ix_min = p.X * Constant.LightPerHeightSize - max_radiate_distance + px_offset;
                    ix_max = ix_min + 2 * max_radiate_distance + 1;
                    ix_center = ix_min + max_radiate_distance;
                    iy_min = p.Y * Constant.LightPerHeightSize - max_radiate_distance + px_offset;
                    iy_max = iy_min + 2 * max_radiate_distance + 1;
                    iy_center = iy_min + max_radiate_distance;
                    for (int y = iy_min; y <= iy_max; ++y)
                    {
                        for (int x = ix_min; x <= ix_max; ++x)
                        {
                            if (!LightGrid.Contains(x, y) || !HeightGrid[x / Constant.LightPerHeightSize, y / Constant.LightPerHeightSize].IsInWorld())
                            {
                                continue;
                            }
                            float value = MathF.Max(MathF.Abs(x - ix_center), MathF.Abs(y - iy_center)) * step_width;
                            float v = LightGrid[x, y];
                            if (value >= 0.0F && v > value)
                            {
                                LightGrid[x, y] = value;
                            }
                        }
                    }
                    c_rad++;
                }
            }
            if (this.Files.LogDebug())
            {
                Debug.WriteLine("initialize grid:" + c_rad + "radiating pixels...");
            }
        }

        /// multithreaded running function for the resource unit level establishment
        private void SaplingEstablishment(ResourceUnit unit)
        {
            this.Saplings.Establishment(unit, this);
        }

        /// multithreaded running function for the resource unit level establishment
        private void SaplingGrowth(ResourceUnit unit)
        {
            this.Saplings.SaplingGrowth(unit, this);
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
        private void Grow(ResourceUnit ru)
        {
            ru.BeforeGrow(); // reset statistics
            // calculate light responses
            // responses are based on *modified* values for LightResourceIndex
            foreach (Trees treesOfSpecies in ru.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    treesOfSpecies.CalcLightResponse(this, treeIndex);
                }
            }
            ru.CalculateInterceptedArea();

            foreach (Trees treesOfSpecies in ru.TreesBySpeciesID.Values)
            {
                for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                {
                    treesOfSpecies.Grow(this, treeIndex); // actual growth of individual trees
                }
            }

            //this.GlobalSettings.SystemStatistics.TreeCount += unit.Trees.Count;
        }

        /// multithreaded running function for the resource level production
        private void Production(ResourceUnit unit)
        {
            unit.Production(this);
        }

        /// multithreaded execution of the carbon cycle routine
        private void CarbonCycle(ResourceUnit unit)
        {
            // (1) do calculations on snag dynamics for the resource unit
            // (2) do the soil carbon and nitrogen dynamics calculations (ICBM/2N)
            unit.CalculateCarbonCycle(this);
        }

        public ResourceUnit FirstResourceUnit()
        {
            return ResourceUnits[0];
        }

        public ResourceUnit GetResourceUnit(PointF coord) // ressource unit at given coordinates
        {
            if (!ResourceUnitGrid.IsEmpty() && ResourceUnitGrid.Contains(coord))
            {
                return ResourceUnitGrid[coord];
            }
            if (ResourceUnitGrid.IsEmpty())
            {
                return FirstResourceUnit(); // default RU if there is only one
            }
            else
            {
                return null; // in this case, no valid coords were provided
            }
        }

        public ResourceUnit GetResourceUnit(int index)  // get resource unit by index
        {
            return (index >= 0 && index < ResourceUnits.Count) ? ResourceUnits[index] : null;
        }
    }
}
