using iLand.Input.ProjectFile;
using iLand.Tools;
using iLand.Tree;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Xml;

namespace iLand.World
{
    public class Landscape
    {
        public RectangleF Extent { get; init; } // extent of the model, not including surrounding light buffer cells
        public float TotalStockableHectares { get; private set; } // total stockable area of the landscape (ha)

        public DEM? Dem { get; init; }
        public Input.EnvironmentReader Environment { get; init; }
        public GrassCover GrassCover { get; init; }
        public List<ResourceUnit> ResourceUnits { get; init; }

        public Grid<float> LightGrid { get; init; } // this is the global 'LIF'-grid (light patterns) (currently 2x2m)
        public Grid<HeightCell> HeightGrid { get; init; } // stores maximum heights of trees and some flags (currently 10x10m)
        public Grid<ResourceUnit> ResourceUnitGrid { get; init; }
        public MapGrid? StandGrid { get; init; } // retrieve the spatial grid that defines the stands (10m resolution)

        public Landscape(Project projectFile)
        {
            // simple case: create resource units in a regular grid.
            if (projectFile.Model.World.ResourceUnitsAsGrid == false)
            {
                throw new NotImplementedException("For now, /project/world/resourceUnitsAsGrid must be set to true.");
            }

            this.Dem = null;
            this.GrassCover = new GrassCover();
            this.ResourceUnits = new List<ResourceUnit>();
            this.ResourceUnitGrid = new Grid<ResourceUnit>();
            this.StandGrid = null;

            float lightCellSize = projectFile.Model.World.CellSize;
            float worldWidth = projectFile.Model.World.Width;
            float worldHeight = projectFile.Model.World.Height;
            float worldBuffer = projectFile.Model.World.Buffer;
            this.Extent = new RectangleF(0.0F, 0.0F, worldWidth, worldHeight);
            // Debug.WriteLine(String.Format("Setup of the world: {0}x{1} m with {2} m light cell size and {3} m buffer", worldWidth, worldHeight, lightCellSize, worldBuffer));

            RectangleF worldExtentBuffered = new RectangleF(-worldBuffer, -worldBuffer, worldWidth + 2 * worldBuffer, worldHeight + 2 * worldBuffer);
            // Debug.WriteLine("Setup grid rectangle: " + worldExtentBuffered);

            this.LightGrid = new Grid<float>(worldExtentBuffered, lightCellSize);
            this.LightGrid.Fill(1.0F);
            this.HeightGrid = new Grid<HeightCell>(worldExtentBuffered, Constant.LightCellsPerHeightSize * lightCellSize);
            for (int index = 0; index < this.HeightGrid.Count; ++index)
            {
                this.HeightGrid[index] = new HeightCell();
            }

            this.ResourceUnitGrid.Setup(new RectangleF(0.0F, 0.0F, worldWidth, worldHeight), 100.0F); // Grid, that holds positions of resource units
            this.ResourceUnitGrid.FillDefault();

            // setup of the digital elevation map if present
            // Performs bounds tests against height grid, so DEM must be created after grids.
            string? demFileName = projectFile.Model.World.DemFile;
            if (String.IsNullOrEmpty(demFileName) == false)
            {
                this.Dem = new DEM(this, projectFile.GetFilePath(ProjectDirectory.Home, demFileName)); // TODO: stop requiring gis\ prefix in project file
            }

            // load environment (multiple climates, speciesSets, ...
            this.Environment = new Input.EnvironmentReader();

            // setup the spatial location of the project area
            if (projectFile.Model.World.Location != null)
            {
                // setup of spatial location
                float worldOriginX = projectFile.Model.World.Location.X;
                float worldOriginY = projectFile.Model.World.Location.Y;
                float worldOriginZ = projectFile.Model.World.Location.Z;
                float worldRotation = projectFile.Model.World.Location.Rotation;
                this.Environment.GisGrid.SetupTransformation(worldOriginX, worldOriginY, worldOriginZ, worldRotation);
                // Debug.WriteLine("Setup of spatial location: " + worldOriginX + "," + worldOriginY + "," + worldOriginZ + " rotation " + worldRotation);
            }
            else
            {
                this.Environment.GisGrid.SetupTransformation(0.0F, 0.0F, 0.0F, 0.0F);
            }

            if (projectFile.Model.World.EnvironmentEnabled)
            {
                bool isGridEnvironment = String.Equals(projectFile.Model.World.EnvironmentMode, "grid", StringComparison.Ordinal);
                if (isGridEnvironment)
                {
                    string? gridFileName = projectFile.Model.World.EnvironmentGridFile;
                    if (String.IsNullOrEmpty(gridFileName))
                    {
                        throw new XmlException("/projectFile/model/world/environmentGrid not found.");
                    }
                    string gridFilePath = projectFile.GetFilePath(ProjectDirectory.Gis, gridFileName);
                    if (File.Exists(gridFilePath))
                    {
                        this.Environment.SetGridMode(gridFilePath);
                    }
                    else
                    {
                        throw new NotSupportedException(String.Format("File '{0}' specified in key 'environmentGrid' does not exist ('environmentMode' is 'grid').", gridFilePath));
                    }
                }

                this.Environment.LoadFromProjectAndEnvironmentFile(projectFile);
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

            bool hasStandGrid = false;
            if (projectFile.Model.World.StandGrid.Enabled)
            {
                string? fileName = projectFile.Model.World.StandGrid.FileName;
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
                if (projectFile.Model.Parameter.Torus == false)
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
                    RectangleF ruExtent = this.ResourceUnitGrid.GetCellExtent(this.ResourceUnitGrid.GetCellPosition(ruGridIndex));
                    this.Environment.SetPosition(projectFile, ruExtent.Center()); // if environment is 'disabled' default values from the project file are used.
                    if ((this.Environment.CurrentClimate == null) || (this.Environment.CurrentSpeciesSet == null))
                    {
                        throw new NotSupportedException("Climate or species parameterizations not found for resource unit " + ruGridIndex + ".");
                    }
                    ResourceUnit newRU = new ResourceUnit(projectFile, this.Environment.CurrentClimate, this.Environment.CurrentSpeciesSet, ruGridIndex)
                    {
                        BoundingBox = ruExtent,
                        EnvironmentID = this.Environment.CurrentResourceUnitID, // set id of resource unit in grid mode
                        TopLeftLightPosition = this.LightGrid.GetCellIndex(ruExtent.TopLeft())
                    };
                    newRU.Setup(projectFile, this.Environment);
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
            Debug.WriteLine("Created grid of " + this.ResourceUnits.Count + " resource units in " + this.ResourceUnitGrid.Count + " map cells.");
            this.CalculateStockableArea();

            // setup of the project area mask
            if ((hasStandGrid == false) && projectFile.Model.World.AreaMask.Enabled && (projectFile.Model.World.AreaMask.ImageFile != null)) // TODO: String.IsNullOrEmpty(ImageFile)?
            {
                // to be extended!!! e.g. to load ESRI-style text files....
                // setup a grid with the same size as the height grid...
                Grid<float> worldMask = new Grid<float>(this.HeightGrid.CellsX, this.HeightGrid.CellsY, this.HeightGrid.CellSize);
                string fileName = projectFile.GetFilePath(ProjectDirectory.Gis, projectFile.Model.World.AreaMask.ImageFile);
                Grid.LoadGridFromImage(fileName, worldMask); // fetch from image
                for (int index = 0; index < worldMask.Count; ++index)
                {
                    this.HeightGrid[index].SetInWorld(worldMask[index] > 0.99);
                }
                Debug.WriteLine("loaded project area mask from" + fileName);
            }
            if (this.ResourceUnits.Count == 0)
            {
                throw new NotSupportedException("Setup of Model: no resource units present!");
            }

            // setup of saplings
            if (projectFile.Model.Settings.RegenerationEnabled)
            {
                //mGrid.setup(GlobalSettings.instance().model().grid().metricRect(), GlobalSettings.instance().model().grid().cellsize());
                // mask out out-of-project areas
                GridWindowEnumerator<float> lightRunner = new GridWindowEnumerator<float>(this.LightGrid, this.ResourceUnitGrid.PhysicalExtent);
                while (lightRunner.MoveNext())
                {
                    SaplingCell? saplingCell = this.GetSaplingCell(this.LightGrid.GetCellPosition(lightRunner.CurrentIndex), false, out ResourceUnit _); // false: retrieve also invalid cells
                    if (saplingCell != null)
                    {
                        if (!this.HeightGrid[this.LightGrid.Index5(lightRunner.CurrentIndex)].IsOnLandscape())
                        {
                            saplingCell.State = SaplingCellState.Invalid;
                        }
                        else
                        {
                            saplingCell.State = SaplingCellState.Free;
                        }
                    }
                }
            }

            // setup of the grass cover
            this.GrassCover.Setup(projectFile, this);
        }

        /** calculate for each resource unit the stockable area.
          "stockability" is determined by the isValid flag of resource units which in turn
          is derived from stand grid values.
          */
        private void CalculateStockableArea() // calculate the stockable area for each RU (i.e.: with stand grid values <> -1)
        {
            this.TotalStockableHectares = 0.0F;
            foreach (ResourceUnit ru in this.ResourceUnits)
            {
                //        if (ru.id()==-1) {
                //            ru.setStockableArea(0.);
                //            continue;
                //        }
                GridWindowEnumerator<HeightCell> heightRunner = new GridWindowEnumerator<HeightCell>(this.HeightGrid, ru.BoundingBox);
                int heightCellsInLandscape = 0;
                int heightCellsInRU = 0;
                while (heightRunner.MoveNext())
                {
                    HeightCell current = heightRunner.Current;
                    if (current != null && current.IsOnLandscape())
                    {
                        ++heightCellsInLandscape;
                    }
                    ++heightCellsInRU;
                }

                if (heightCellsInRU < 1)
                {
                    // TODO: check against Constant.HeightSizePerRU * Constant.HeightSizePerRU?
                    throw new NotSupportedException("No height cells found in resource unit.");
                }

                ru.AreaInLandscape = Constant.HeightPixelArea * heightCellsInLandscape; // in m2
                if (ru.Snags != null)
                {
                    ru.Snags.ScaleInitialState();
                }
                this.TotalStockableHectares += Constant.HeightPixelArea * heightCellsInLandscape / Constant.RUArea; // in ha

                if (heightCellsInLandscape == 0 && ru.EnvironmentID > -1)
                {
                    // invalidate this resource unit
                    // ru.ID = -1;
                    throw new NotSupportedException("Valid resource unit has no height cells in world.");
                }
                if (heightCellsInLandscape > 0 && ru.EnvironmentID == -1)
                {
                    throw new NotSupportedException("Invalid resource unit " + ru.ResourceUnitGridIndex + " (" + ru.BoundingBox + ") has height cells in world.");
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
                if (runner.Current.IsOnLandscape() == false)
                {
                    // if the current pixel is a "radiating" border pixel,
                    // then check the neighbors and set a flag if the pixel is a neighbor of a in-project-area pixel.
                    runner.GetNeighbors8(neighbors);
                    for (int neighborIndex = 0; neighborIndex < neighbors.Length; ++neighborIndex)
                    {
                        if (neighbors[neighborIndex] != null && neighbors[neighborIndex].IsOnLandscape())
                        {
                            runner.Current.SetIsRadiating();
                        }
                    }
                }
            }
        }

        public static SqliteConnection GetDatabaseConnection(string databaseFilePath, bool openReadOnly)
        {
            if (openReadOnly)
            {
                if (File.Exists(databaseFilePath) == false)
                {
                    throw new ArgumentException("Database file '" + databaseFilePath + "'does not exist!", nameof(databaseFilePath));
                }
            }

            SqliteConnectionStringBuilder connectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = databaseFilePath,
                Mode = openReadOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            };
            SqliteConnection connection = new SqliteConnection(connectionString.ConnectionString);
            connection.Open();
            if (openReadOnly == false)
            {
                // performance settings for output databases (http://www.sqlite.org/pragma.html)
                // Databases are typically expensive to create and maintain so SQLite defaults to conservative disk interactions. iLand
                // output data is cheap to generate and easy to recreate in the unlikely event something goes wrong flushing to disk, so
                // caution can be exchanged for speed. For example, journal_mode = memory, synchronous = off, and temp_store = memory 
                // make the model unit tests run 4-5x times faster than default settings.
                // pragma synchronous cannot be changed within a transaction
                using SqliteCommand synchronization = new SqliteCommand("pragma synchronous(off)", connection);
                synchronization.ExecuteNonQuery();

                using SqliteTransaction transaction = connection.BeginTransaction();
                // little to no difference between journal_mode = memory and journal_mode = off
                using SqliteCommand journalMode = new SqliteCommand("pragma journal_mode(memory)", connection, transaction);
                journalMode.ExecuteNonQuery();
                using SqliteCommand tempStore = new SqliteCommand("pragma temp_store(memory)", connection, transaction);
                tempStore.ExecuteNonQuery();
                transaction.Commit();
            }

            return connection;
        }

        public ResourceUnit GetResourceUnit(PointF ruPosition) // resource unit at given coordinates
        {
            if (this.ResourceUnitGrid.IsNotSetup())
            {
                // TODO: why not just populate grid with the default resource unit?
                return this.ResourceUnits[0]; // default RU if there is only one
            }
            return this.ResourceUnitGrid[ruPosition];
        }

        /// return the SaplingCell (i.e. container for the ind. saplings) for the given 2x2m coordinates
        /// if 'only_valid' is true, then null is returned if no living saplings are on the cell
        /// 'rRUPtr' is a pointer to a RU-ptr: if provided, a pointer to the resource unit is stored
        public SaplingCell? GetSaplingCell(Point lightCellPosition, bool onlyValid, out ResourceUnit ru)
        {
            ru = this.GetResourceUnit(this.LightGrid.GetCellCenterPosition(lightCellPosition));
            SaplingCell saplingCell = ru.GetSaplingCell(lightCellPosition);
            if ((saplingCell != null) && (!onlyValid || saplingCell.State != SaplingCellState.Invalid))
            {
                return saplingCell;
            }
            return null;
        }
    }
}
