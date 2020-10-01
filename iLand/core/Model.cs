using iLand.Tools;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Xml;

namespace iLand.Core
{
    public class Model : IDisposable
    {
        private bool isDisposed;
        /// container holding all species sets
        private readonly List<SpeciesSet> mSpeciesSets;

        // access to elements
        public ThreadRunner ThreadRunner { get; private set; }
        public RectangleF WorldExtentUnbuffered { get; private set; } ///< extent of the model (without buffer)
        public double TotalStockableArea { get; private set; } ///< total stockable area of the landscape (ha)

        public List<ResourceUnit> ResourceUnits { get; private set; }
        public Management Management { get; private set; }
        public Environment Environment { get; private set; }
        public Saplings Saplings { get; private set; }
        public TimeEvents TimeEvents { get; private set; }
        public Modules Modules { get; private set; }
        public DEM Dem { get; private set; }
        public GrassCover GrassCover { get; private set; }
        public List<Climate> Climates { get; private set; }

        public bool IsSetup { get; private set; } ///< return true if the model world is correctly setup.

        // global grids
        public Grid<float> LightGrid { get; private set; } ///< this is the global 'LIF'-grid (light patterns) (currently 2x2m)
        public Grid<HeightGridValue> HeightGrid { get; private set; } ///< stores maximum heights of trees and some flags (currently 10x10m)
        public MapGrid StandGrid { get; private set; } ///< retrieve the spatial grid that defines the stands (10m resolution)
        public Grid<ResourceUnit> ResourceUnitGrid { get; private set; }

        public ModelSettings Settings { get; private set; }

        public Model()
        {
            GlobalSettings.Instance.CurrentYear = 0; // BUGBUG
            GlobalSettings.Instance.Model = this; // BUGBUG: many to one set

            this.Climates = new List<Climate>();
            this.ResourceUnits = new List<ResourceUnit>();
            this.ResourceUnitGrid = new Grid<ResourceUnit>();
            this.mSpeciesSets = new List<SpeciesSet>();
            this.Settings = new ModelSettings();
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
        public void AfterStop() ///< finish and cleanup
        {
            // do some cleanup
            // no op in C++ iLand sources
        }

        /// beforeRun performs several steps before the models starts running.
        /// inter alia: * setup of the stands
        ///             * setup of the climates
        public void BeforeRun() ///< initializations
        {
            // setup outputs
            // setup output database
            if ((GlobalSettings.Instance.DatabaseOutput != null) && (GlobalSettings.Instance.DatabaseOutput.State != ConnectionState.Closed))
            {
                GlobalSettings.Instance.DatabaseOutput.Close();
            }
            OpenOutputDatabase();
            GlobalSettings.Instance.OutputManager.Setup();
            GlobalSettings.Instance.ClearDebugLists();

            // initialize stands
            StandLoader loader = new StandLoader(this);
            {
                using DebugTimer loadTrees = new DebugTimer("StandLoader.ProcessInit()");
                loader.ProcessInit();
            }

            // load climate
            {
                if (GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("attempting to load climate...");
                }
                using DebugTimer loadClimamtes = new DebugTimer("Model.BeforeRun(Climate.Setup + NextYear)");
                foreach (Climate c in Climates)
                {
                    if (!c.IsSetup)
                    {
                        c.Setup();
                    }
                }
                // load the first year of the climate database
                foreach (Climate c in Climates)
                {
                    c.NextYear();
                }
            }

            {
                if (GlobalSettings.Instance.LogDebug())
                {
                    Debug.WriteLine("attempting to calculate initial stand statistics (incl. apply and read pattern)...");
                }
                using DebugTimer loadinit = new DebugTimer("Model.BeforeRun(light + height grids and statistics)");
                Tree.SetGrids(LightGrid, HeightGrid);
                // debugCheckAllTrees(); // introduced for debugging session (2012-04-06)
                ApplyPattern();
                ReadPattern();
                loader.ProcessAfterInit(); // e.g. initialization of saplings

                // force the compilation of initial stand statistics
                CreateStandStatistics();
            }

            // outputs to create with inital state (without any growth) are called here:
            GlobalSettings.Instance.CurrentYear = 0; // set clock to "0" (for outputs with initial state)
            GlobalSettings.Instance.OutputManager.LogYear(); // log initial state
            GlobalSettings.Instance.CurrentYear = 1; // set to first year
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
        public void RunYear() ///< run a single year
        {
            using DebugTimer t = new DebugTimer("Model.RunYear()");
            GlobalSettings.Instance.SystemStatistics.Reset();
            RandomGenerator.CheckGenerator(); // see if we need to generate new numbers...
                                              // initalization at start of year for external modules
            Modules.YearBegin();

            // execute scheduled events for the current year
            if (TimeEvents != null)
            {
                TimeEvents.Run();
            }
            // load the next year of the climate database (except for the first year - the first climate year is loaded immediately
            if (GlobalSettings.Instance.CurrentYear > 1)
            {
                foreach (Climate c in Climates)
                {
                    c.NextYear();
                }
            }

            // reset statistics
            foreach (ResourceUnit ru in ResourceUnits)
            {
                ru.NewYear();
            }

            foreach (SpeciesSet set in mSpeciesSets)
            {
                set.NewYear();
            }
            // management classic
            if (Management != null)
            {
                using DebugTimer t2 = new DebugTimer("Management.Run()");
                Management.Run();
                GlobalSettings.Instance.SystemStatistics.ManagementTime += t.Elapsed();
            }

            // if trees are dead/removed because of management, the tree lists
            // need to be cleaned (and the statistics need to be recreated)
            CleanTreeLists(true); // recalculate statistics (LAIs per species needed later in production)

            // process a cycle of individual growth
            ApplyPattern(); // create Light Influence Patterns
            ReadPattern(); // readout light state of individual trees
            Grow(); // let the trees grow (growth on stand-level, tree-level, mortality)
            GrassCover.Execute(); // evaluate the grass / herb cover (and its effect on regeneration)

            // regeneration
            if (Settings.RegenerationEnabled)
            {
                // seed dispersal
                using DebugTimer tseed = new DebugTimer("Model.RunYear(seed dispersal, establishment, sapling growth");
                foreach (SpeciesSet set in mSpeciesSets)
                {
                    set.Regeneration(); // parallel execution for each species set
                }
                GlobalSettings.Instance.SystemStatistics.SeedDistributionTime += tseed.Elapsed();
                // establishment
                Saplings.UpdateBrowsingPressure();

                {
                    using DebugTimer t2 = new DebugTimer("Model.SaplingEstablishment()");
                    ExecutePerResourceUnit(SaplingEstablishment, false /* true: force single threaded operation */);
                    GlobalSettings.Instance.SystemStatistics.EstablishmentTime += t.Elapsed();
                }
                {
                    using DebugTimer t3 = new DebugTimer("Model.SaplingGrowth()");
                    ExecutePerResourceUnit(SaplingGrowth, false /* true: force single threaded operation */);
                    GlobalSettings.Instance.SystemStatistics.SaplingTime += t.Elapsed();
                }

                // Establishment.debugInfo(); // debug test
            }

            // external modules/disturbances
            Modules.Run();
            // cleanup of tree lists if external modules removed trees.
            CleanTreeLists(false); // do not recalculate statistics - this is done in ru.yearEnd()


            // calculate soil / snag dynamics
            if (Settings.CarbonCycleEnabled)
            {
                using DebugTimer ccycle = new DebugTimer("Model.CarbonCycle90");
                ExecutePerResourceUnit(CarbonCycle, false /* true: force single threaded operation */);
                GlobalSettings.Instance.SystemStatistics.CarbonCycleTime += ccycle.Elapsed();
            }

            using DebugTimer toutput = new DebugTimer("Model.RunYear(outputs)");
            // calculate statistics
            foreach (ResourceUnit ru in ResourceUnits)
            {
                ru.YearEnd();
            }
            // create outputs
            GlobalSettings.Instance.OutputManager.LogYear();

            GlobalSettings.Instance.SystemStatistics.WriteOutputTime += toutput.Elapsed();
            GlobalSettings.Instance.SystemStatistics.TotalYearTime += t.Elapsed();
            GlobalSettings.Instance.SystemStatistics.WriteOutput();

            ++GlobalSettings.Instance.CurrentYear;
        }

        // setup/maintenance
        /** Clear() frees all ressources allocated with the run of a simulation.
          */
        public void Clear() ///< free resources
        {
            IsSetup = false;
            Debug.WriteLine("Model clear: attempting to clear " + ResourceUnits.Count + "RU, " + mSpeciesSets.Count + " SpeciesSets.");

            ResourceUnits.Clear();
            mSpeciesSets.Clear();
            Climates.Clear();

            LightGrid = null;
            HeightGrid = null;
            Management = null;
            Environment = null;
            TimeEvents = null;
            StandGrid = null;
            Modules = null;
            Dem = null;
            GrassCover = null;

            Debug.WriteLine("Model resources freed.");
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
                    GlobalSettings.Instance.Model = null;
                    Clear();
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
        public void LoadProject() ///< setup and load a project
        {
            using DebugTimer dt = new DebugTimer("Model.LoadProject()");
            // GlobalSettings.Instance.PrintDirectories();
            XmlHelper xml = GlobalSettings.Instance.Settings;

            // log level
            string logLevelAsString = xml.GetString("system.settings.logLevel", "debug").ToLowerInvariant();
            int logLevel = logLevelAsString switch
            {
                "debug" => 0,
                "info" => 1,
                "warning" => 2,
                "error" => 3,
                _ => throw new NotSupportedException("Unhandled log level '" + logLevelAsString + "'.")
            };
            GlobalSettings.Instance.SetLogLevel(logLevel);

            // database connections: reset
            GlobalSettings.Instance.ClearDatabaseConnections();
            // input and climate connection
            // see initOutputDatabase() for output database
            string dbPath = GlobalSettings.Instance.Path(xml.GetString("system.database.in"), "database");
            GlobalSettings.Instance.SetupDatabaseConnection("in", dbPath, true);
            dbPath = GlobalSettings.Instance.Path(xml.GetString("system.database.climate"), "database");
            GlobalSettings.Instance.SetupDatabaseConnection("climate", dbPath, true);

            Settings.LoadModelSettings();
            Settings.Print();
            // random seed: if stored value is <> 0, use this as the random seed (and produce hence always an equal sequence of random numbers)
            int seed = Int32.Parse(xml.GetString("system.settings.randomSeed", "0"));
            RandomGenerator.Setup(RandomGenerator.RandomGenerators.MersenneTwister, seed); // use the MersenneTwister as default
                                                                                           // linearization of expressions: if true *and* linearize() is explicitely called, then
                                                                                           // function results will be cached over a defined range of values.
            bool do_linearization = xml.GetBool("system.settings.expressionLinearizationEnabled", false);
            Expression.LinearizationEnabled = do_linearization;
            if (do_linearization)
            {
                Debug.WriteLine("Linearization of expressions is enabled to reduce computation.");
            }

            // snag dynamics / soil model enabled? (info used during setup of world)
            Settings.CarbonCycleEnabled = xml.GetBool("model.settings.carbonCycleEnabled", false);
            // class size of snag classes
            Snag.SetupThresholds(xml.GetDouble("model.settings.soil.swdDBHClass12"),
                                 xml.GetDouble("model.settings.soil.swdDBHClass23"));

            // setup of modules
            Modules = new Modules();

            Settings.RegenerationEnabled = xml.GetBool("model.settings.regenerationEnabled", false);

            SetupSpace();
            if (ResourceUnits.Count == 0)
            {
                throw new NotSupportedException("Setup of Model: no resource units present!");
            }

            // (3) additional issues
            // (3.2) setup of regeneration
            if (Settings.RegenerationEnabled)
            {
                foreach (SpeciesSet ss in mSpeciesSets)
                {
                    ss.SetupRegeneration();
                }
            }
            Saplings.RecruitmentVariation = xml.GetDouble("model.settings.seedDispersal.recruitmentDimensionVariation", 0.1);

            if (xml.GetBool("model.management.enabled"))
            {
                Management = new Management();
                string mgmtFile = xml.GetString("model.management.file");
                string path = GlobalSettings.Instance.Path(mgmtFile, "script");
            }
        }

        /// get the value of the (10m) Height grid at the position index ix and iy (of the LIF grid)
        public HeightGridValue HeightGridValue(int ix, int iy)
        {
            return HeightGrid[ix / Constant.LightPerHeightSize, iy / Constant.LightPerHeightSize];
        }

        public SpeciesSet SpeciesSet()
        {
            if (mSpeciesSets.Count == 1)
            {
                return mSpeciesSets[0];
            }
            return null;
        }

        // actions
        /// build stand statistics (i.e. stats based on resource units)
        public void CreateStandStatistics()
        {
            CalculateStockedArea();
            foreach (ResourceUnit ru in ResourceUnits)
            {
                ru.AddTreeAgingForAllTrees();
                ru.CreateStandStatistics();
            }
        }

        /// clean the tree data structures (remove harvested trees) - call after management operations.
        public void CleanTreeLists(bool recalculate_stats)
        {
            foreach (ResourceUnit ru in GlobalSettings.Instance.Model.ResourceUnits)
            {
                if (ru.HasDeadTrees)
                {
                    ru.CleanTreeList();
                    ru.RecreateStandStatistics(recalculate_stats);
                }
            }
        }

        /// execute a function for each resource unit using multiple threads. "funcptr" is a ptr to a simple function
        public void ExecutePerResourceUnit(Action<ResourceUnit> funcptr, bool forceSingleThreaded = false)
        {
            ThreadRunner.Run(funcptr, forceSingleThreaded);
        }

        private void SetupSpace() ///< setup the "world"(spatial grids, ...), create ressource units
        {
            XmlHelper xml = new XmlHelper(GlobalSettings.Instance.Settings.Node("model.world"));
            float cellSize = (float)xml.GetDouble("cellSize", Constant.LightSize);
            float width = (float)xml.GetDouble("width", Constant.RUSize); // default to a single resource unit
            float height = (float)xml.GetDouble("height", Constant.RUSize);
            float buffer = (float)xml.GetDouble("buffer", 0.6 * Constant.RUSize);
            this.WorldExtentUnbuffered = new RectangleF(0.0F, 0.0F, width, height);

            Debug.WriteLine(String.Format("Setup of the world: {0}x{1} m with {2} m cell size and {3} m buffer", width, height, cellSize, buffer));

            RectangleF worldExtentBuffered = new RectangleF(new PointF(-buffer, -buffer), new SizeF(width + buffer, height + buffer));
            Debug.WriteLine("Setup grid rectangle: " + worldExtentBuffered);

            this.LightGrid = new Grid<float>(worldExtentBuffered, cellSize);
            this.LightGrid.Initialize(1.0F);
            this.HeightGrid = new Grid<HeightGridValue>(worldExtentBuffered, cellSize * Constant.LightPerHeightSize);
            for (int index = 0; index < this.HeightGrid.Count; ++index)
            {
                this.HeightGrid[index] = new HeightGridValue();
            }
            Tree.SetGrids(LightGrid, HeightGrid);

            // setup the spatial location of the project area
            if (xml.HasNode("location"))
            {
                // setup of spatial location
                double loc_x = xml.GetDouble("location.x");
                double loc_y = xml.GetDouble("location.y");
                double loc_z = xml.GetDouble("location.z");
                double loc_rot = xml.GetDouble("location.rotation");
                GisGrid.SetupGISTransformation(loc_x, loc_y, loc_z, loc_rot);
                Debug.WriteLine("Setup of spatial location: " + loc_x + "," + loc_y + "," + loc_z + " rotation " + loc_rot);
            }
            else
            {
                GisGrid.SetupGISTransformation(0.0, 0.0, 0.0, 0.0);
            }

            // load environment (multiple climates, speciesSets, ...
            Environment = new Environment();

            if (xml.GetBool("environmentEnabled", false))
            {
                bool isGridEnvironment = xml.GetString("environmentMode") == "grid";
                if (isGridEnvironment)
                {
                    string gridFileName = xml.GetString("environmentGrid");
                    if (String.IsNullOrEmpty(gridFileName))
                    {
                        throw new XmlException("/project/model/world/environmentGrid not found.");
                    }
                    string gridFilePath = GlobalSettings.Instance.Path(gridFileName);
                    if (File.Exists(gridFilePath) && String.IsNullOrEmpty(xml.GetString("environmentGrid")) == false)
                    {
                        Environment.SetGridMode(gridFilePath);
                    }
                    else
                    {
                        throw new NotSupportedException(String.Format("File '{0}' specified in key 'environmentGrid' does not exist ('environmentMode' is 'grid').", gridFilePath));
                    }
                }

                string environmentFileName = xml.GetString("environmentFile");
                if (String.IsNullOrEmpty(environmentFileName))
                {
                    throw new XmlException("/project/model/world/environmentFile not found.");
                }
                string environmentFilePath = GlobalSettings.Instance.Path(environmentFileName);
                if (!Environment.LoadFromFile(environmentFilePath))
                {
                    return;
                }
            }
            else
            {
                // load and prepare default values
                // (2) SpeciesSets: currently only one a global species set.
                SpeciesSet speciesSet = new SpeciesSet();
                mSpeciesSets.Add(speciesSet);
                speciesSet.Setup();
                // Climate...
                Climate c = new Climate();
                Climates.Add(c);
                Environment.SetDefaultValues(c, speciesSet);
            } // environment?

            // time series data
            if (xml.GetBool(".timeEventsEnabled", false))
            {
                TimeEvents = new TimeEvents();
                string timeEventsFileName = xml.GetString("timeEventsFile");
                if (String.IsNullOrEmpty(timeEventsFileName))
                {
                    throw new XmlException("/project/model/world/timeEventsFile not found");
                }
                TimeEvents.LoadFromFile(GlobalSettings.Instance.Path(timeEventsFileName, "script"));
            }

            // simple case: create resource units in a regular grid.
            if (xml.GetBool("resourceUnitsAsGrid") == false)
            {
                throw new XmlException("/project/world/resourceUnitsAsGrid MUST be set to true - at least currently :)");
            }

            ResourceUnitGrid.Setup(new RectangleF(0.0F, 0.0F, width, height), 100.0); // Grid, that holds positions of resource units
            ResourceUnitGrid.ClearDefault();

            bool mask_is_setup = false;
            if (xml.GetBool("standGrid.enabled"))
            {
                string fileName = GlobalSettings.Instance.Path(xml.GetString("standGrid.fileName"));
                StandGrid = new MapGrid(fileName, false); // create stand grid index later

                if (StandGrid.IsValid())
                {
                    for (int i = 0; i < StandGrid.Grid.Count; i++)
                    {
                        int grid_value = StandGrid.Grid[i];
                        HeightGrid[i].SetValid(grid_value > -1);
                        // BUGBUG: unclear why this is present in C++, appears removable
                        //if (grid_value > -1)
                        //{
                        //    mRUmap[mStandGrid.grid().cellCenterPoint(i)] = (ResourceUnit)1;
                        //}
                        if (grid_value < -1)
                        {
                            HeightGrid[i].SetForestOutside(true);
                        }
                    }
                }
                mask_is_setup = true;
            }
            else
            {
                if (!Settings.TorusMode)
                {
                    // in the case we have no stand grid but only a large rectangle (without the torus option)
                    // we assume a forest outside
                    for (int i = 0; i < HeightGrid.Count; ++i)
                    {
                        PointF p = HeightGrid.GetCellCenterPoint(HeightGrid.IndexOf(i));
                        if (p.X < 0.0F || p.X > width || p.Y < 0.0F || p.Y > height)
                        {
                            HeightGrid[i].SetForestOutside(true);
                            HeightGrid[i].SetValid(false);
                        }
                    }
                }
            }

            int ru_index = 0;
            for (int p = 0; p < ResourceUnitGrid.Count; ++p)
            {
                RectangleF r = ResourceUnitGrid.GetCellRect(ResourceUnitGrid.IndexOf(p));
                if (StandGrid == null || !StandGrid.IsValid())
                {
                    Environment.SetPosition(r.Center()); // if environment is 'disabled' default values from the project file are used.
                                                         // create resource units for valid positions only
                    ResourceUnit new_ru = new ResourceUnit(ru_index++) // create resource unit
                    {
                        Climate = Environment.CurrentClimate
                    };
                    new_ru.SetSpeciesSet(Environment.CurrentSpeciesSet);
                    new_ru.Setup();
                    new_ru.ID = Environment.CurrentID; // set id of resource unit in grid mode
                    new_ru.SetBoundingBox(r);
                    ResourceUnits.Add(new_ru);
                    ResourceUnitGrid[p] = new_ru; // save in the RUmap grid
                }
            }
            if (Environment != null)
            {
                // retrieve species sets and climates (that were really used)
                mSpeciesSets.AddRange(Environment.SpeciesSets);
                Climates.AddRange(Environment.Climates);
                StringBuilder climate_file_list = new StringBuilder();
                for (int i = 0, c = 0; i < Climates.Count; ++i)
                {
                    climate_file_list.Append(Climates[i].Name + ", ");
                    if (++c > 5)
                    {
                        climate_file_list.Append("...");
                        break;
                    }

                }
                // Debug.WriteLine("Setup of climates: #loaded: " + Climates.Count + " tables: " + climate_file_list);
            }

            Debug.WriteLine("Setup of " + this.Environment.Climates.Count + " climate(s) performed.");

            if (StandGrid != null && StandGrid.IsValid())
            {
                StandGrid.CreateIndex();
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
            if (!mask_is_setup && xml.GetBool("areaMask.enabled", false) && xml.HasNode("areaMask.imageFile"))
            {
                // to be extended!!! e.g. to load ESRI-style text files....
                // setup a grid with the same size as the height grid...
                Grid<float> tempgrid = new Grid<float>((int)HeightGrid.CellSize, HeightGrid.CellsX, HeightGrid.CellsY);
                string fileName = GlobalSettings.Instance.Path(xml.GetString("areaMask.imageFile"));
                Grid.LoadGridFromImage(fileName, tempgrid); // fetch from image
                for (int i = 0; i < tempgrid.Count; i++)
                {
                    HeightGrid[i].SetValid(tempgrid[i] > 0.99);
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
            string dem_file = xml.GetString("DEM");
            if (String.IsNullOrEmpty(dem_file) == false)
            {
                this.Dem = new DEM(GlobalSettings.Instance.Path(dem_file));
            }

            // setup of saplings
            if (Saplings != null)
            {
                Saplings = null;
            }
            if (Settings.RegenerationEnabled)
            {
                Saplings = new Saplings();
                Saplings.Setup();
            }

            // setup of the grass cover
            if (GrassCover == null)
            {
                GrassCover = new GrassCover();
            }
            GrassCover.Setup();

            // setup of external modules
            Modules.Setup();
            if (Modules.HasSetupResourceUnits())
            {
                for (int index = 0; index < ResourceUnitGrid.Count; ++index)
                {
                    ResourceUnit p = ResourceUnitGrid[index];
                    if (p != null)
                    {
                        RectangleF r = ResourceUnitGrid.GetCellRect(ResourceUnitGrid.IndexOf(p));
                        Environment.SetPosition(r.Center()); // if environment is 'disabled' default values from the project file are used.
                        Modules.SetupResourceUnit(p);
                    }
                }
            }

            // setup the helper that does the multithreading
            ThreadRunner.Setup(valid_rus);
            ThreadRunner.IsMultithreaded = GlobalSettings.Instance.Settings.GetBool("system.settings.multithreading");
            // Debug.WriteLine("Multithreading enabled: " + IsMultithreaded + ", thread count: " + System.Environment.ProcessorCount);

            IsSetup = true;
        }

        private void OpenOutputDatabase() ///< setup output database (run metadata, ...)
        {
            // create run-metadata
            //int maxID = (int)(long)SqlHelper.QueryValue("select max(id) from runs", g.DatabaseInput);
            //maxID++;
            //SqlHelper.ExecuteSql(String.Format("insert into runs (id, timestamp) values ({0}, '{1}')", maxID, timestamp), g.DatabaseInput);
            // replace path information
            // setup final path
            GlobalSettings globalSettings = GlobalSettings.Instance;
            string outputDatabaseFile = globalSettings.Settings.GetString("system.database.out");
            if (String.IsNullOrWhiteSpace(outputDatabaseFile))
            {
                throw new XmlException("The /project/system/database/out element is missing or does not specify an output database file name.");
            }
            string outputDatabasePath = globalSettings.Path(outputDatabaseFile, "output");
            // dbPath.Replace("$id$", maxID.ToString(), StringComparison.Ordinal);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_hhmmss");
            outputDatabasePath.Replace("$date$", timestamp, StringComparison.Ordinal);
            globalSettings.SetupDatabaseConnection("out", outputDatabasePath, false);
        }

        private void ApplyPattern() ///< apply LIP-patterns of all trees
        {
            using DebugTimer t = new DebugTimer("Model.ApplyPattern()");
            // intialize grids...
            InitializeGrid();

            // initialize height grid with a value of 4m. This is the height of the regeneration layer
            for (int h = 0; h < HeightGrid.Count; ++h)
            {
                HeightGrid[h].ResetCount(); // set count = 0, but do not touch the flags
                HeightGrid[h].Height = 4.0F;
            }

            ThreadRunner.Run(CalculateHeightFieldAndLightIntensityPattern);
            GlobalSettings.Instance.SystemStatistics.ApplyPatternTime += t.Elapsed();
        }

        private void ReadPattern() ///< retrieve LRI for trees
        {
            using DebugTimer t = new DebugTimer("Model.ReadPattern()");
            ThreadRunner.Run(ReadPattern);
            GlobalSettings.Instance.SystemStatistics.ReadPatternTime += t.Elapsed();
        }

        /** Main function for the growth of stands and trees.
           This includes several steps.
           (1) calculate the stocked area (i.e. count pixels in height grid)
           (2) 3PG production (including response calculation, water cycle)
           (3) single tree growth (including mortality)
           (4) cleanup of tree lists (remove dead trees)
          */
        private void Grow() ///< grow - both on RU-level and tree-level
        {
            {
                using DebugTimer t = new DebugTimer("Model.Production()");
                CalculateStockedArea();

                // Production of biomass (stand level, 3PG)
                ThreadRunner.Run(Production);
            }

            using DebugTimer t2 = new DebugTimer("Model.Grow()");
            ThreadRunner.Run(Grow); // actual growth of individual trees

            foreach (ResourceUnit ru in ResourceUnits)
            {
                ru.CleanTreeList();
                ru.AfterGrow();
                //Debug.WriteLine((b-n) + "trees died (of" + b + ").");
            }
            GlobalSettings.Instance.SystemStatistics.TreeGrowthTime += t2.Elapsed();
        }

        /** calculate for each resource unit the fraction of area which is stocked.
          This is done by checking the pixels of the global height grid.
          */
        private void CalculateStockedArea() ///< calculate area stocked with trees for each RU
        {
            // iterate over the whole heightgrid and count pixels for each ressource unit
            for (int i = 0; i < HeightGrid.Count; ++i)
            {
                PointF cp = HeightGrid.GetCellCenterPoint(i);
                if (ResourceUnitGrid.Contains(cp))
                {
                    ResourceUnit ru = ResourceUnitGrid[cp];
                    if (ru != null)
                    {
                        ru.AddHeightCell(HeightGrid[i].Count() > 0);
                    }
                }
            }
        }

        /** calculate for each resource unit the stockable area.
          "stockability" is determined by the isValid flag of resource units which in turn
          is derived from stand grid values.
          */
        private void CalculateStockableArea() ///< calculate the stockable area for each RU (i.e.: with stand grid values <> -1)
        {
            TotalStockableArea = 0.0;
            foreach (ResourceUnit ru in ResourceUnits)
            {
                // //
                //        if (ru.id()==-1) {
                //            ru.setStockableArea(0.);
                //            continue;
                //        }
                GridRunner<HeightGridValue> heightRunner = new GridRunner<HeightGridValue>(HeightGrid, ru.BoundingBox);
                int valid = 0;
                int total = 0;
                for (heightRunner.MoveNext(); heightRunner.IsValid(); heightRunner.MoveNext())
                {
                    HeightGridValue current = heightRunner.Current;
                    if (current != null && current.IsValid())
                    {
                        valid++;
                    }
                    total++;
                }
                if (total != 0)
                {
                    ru.StockableArea = Constant.HeightPixelArea * valid; // in m2
                    if (ru.Snags != null)
                    {
                        ru.Snags.ScaleInitialState();
                    }
                    TotalStockableArea += Constant.HeightPixelArea * valid / Constant.RUArea; // in ha
                    if (valid == 0 && ru.ID > -1)
                    {
                        // invalidate this resource unit
                        ru.ID = -1;
                    }
                    if (valid > 0 && ru.ID == -1)
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
            GridRunner<HeightGridValue> runner = new GridRunner<HeightGridValue>(HeightGrid, HeightGrid.PhysicalExtent);
            HeightGridValue[] neighbors = new HeightGridValue[8];
            for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
            {
                if (runner.Current.IsForestOutside())
                {
                    // if the current pixel is a "radiating" border pixel,
                    // then check the neighbors and set a flag if the pixel is a neighbor of a in-project-area pixel.
                    runner.Neighbors8(neighbors);
                    for (int i = 0; i < 8; ++i)
                    {
                        if (neighbors[i] != null && neighbors[i].IsValid())
                        {
                            runner.Current.SetIsRadiating();
                        }
                    }
                }
            }

            Debug.WriteLine("Total stockable area of the landscape is" + TotalStockableArea + "ha.");
        }

        private void InitializeGrid() ///< initialize the LIF grid
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
                HeightGridValue hgv = HeightGrid[index];
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
                            if (!LightGrid.Contains(x, y) || !HeightGrid[x / Constant.LightPerHeightSize, y / Constant.LightPerHeightSize].IsValid())
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
            if (GlobalSettings.Instance.LogDebug())
            {
                Debug.WriteLine("initialize grid:" + c_rad + "radiating pixels...");
            }
        }

        /// multithreaded running function for the resource unit level establishment
        private void SaplingEstablishment(ResourceUnit unit)
        {
            Saplings s = GlobalSettings.Instance.Model.Saplings;
            s.Establishment(unit);
        }

        /// multithreaded running function for the resource unit level establishment
        private void SaplingGrowth(ResourceUnit unit)
        {
            Saplings s = GlobalSettings.Instance.Model.Saplings;
            s.SaplingGrowth(unit);
        }

        /// multithreaded running function for LIP printing
        private void CalculateHeightFieldAndLightIntensityPattern(ResourceUnit unit)
        {
            List<Tree> trees = unit.Trees;
            // light concurrence influence
            if (GlobalSettings.Instance.Model.Settings.TorusMode)
            {
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].CalculateDominantHeightFieldTorus();
                }
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].ApplyLightIntensityPatternTorus();
                }
            }
            else
            {
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].CalculateDominantHeightField();
                }
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].ApplyLightIntensityPattern();
                }
            }
        }

        /// multithreaded running function for LIP value extraction
        private void ReadPattern(ResourceUnit unit)
        {
            List<Tree> trees = unit.Trees;
            if (!GlobalSettings.Instance.Model.Settings.TorusMode)
            {
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].ReadLightInfluenceField(); // multiplicative approach
                }
            }
            else
            {
                for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                {
                    trees[treeIndex].ReadLightIntensityFieldTorus();
                }
            }
        }

        /// multithreaded running function for the growth of individual trees
        private void Grow(ResourceUnit unit)
        {
            unit.BeforeGrow(); // reset statistics
            // calculate light responses
            // responses are based on *modified* values for LightResourceIndex
            List<Tree> trees = unit.Trees;
            for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
            {
                trees[treeIndex].CalcLightResponse();
            }
            unit.CalculateInterceptedArea();

            for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
            {
                trees[treeIndex].Grow(); // actual growth of individual trees
            }

            GlobalSettings.Instance.SystemStatistics.TreeCount += unit.Trees.Count;
        }

        /// multithreaded running function for the resource level production
        private void Production(ResourceUnit unit)
        {
            unit.Production();
        }

        /// multithreaded execution of the carbon cycle routine
        private void CarbonCycle(ResourceUnit unit)
        {
            // (1) do calculations on snag dynamics for the resource unit
            // (2) do the soil carbon and nitrogen dynamics calculations (ICBM/2N)
            unit.CalculateCarbonCycle();
        }

        public ResourceUnit FirstResourceUnit()
        {
            return ResourceUnits[0];
        }

        public ResourceUnit GetResourceUnit(PointF coord) ///< ressource unit at given coordinates
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

        public ResourceUnit GetResourceUnit(int index)  ///< get resource unit by index
        {
            return (index >= 0 && index < ResourceUnits.Count) ? ResourceUnits[index] : null;
        }
    }
}
