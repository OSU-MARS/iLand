using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Input.Tree;
using iLand.Tool;
using iLand.Tree;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace iLand.World
{
    public class Landscape
    {
        public Dictionary<string, WeatherDaily> WeatherByID { get; private init; }

        public GrassCover GrassCover { get; private init; }
        public Grid<float> LightGrid { get; private init; } // this is the global 'LIF'-grid (light patterns) (currently 2x2m)
        public Grid<HeightCell> HeightGrid { get; private init; } // stores maximum heights of trees and some flags (currently 10x10m)
        public PointF ProjectOriginInGisCoordinates { get; private init; }

        public Grid<ResourceUnit?> ResourceUnitGrid { get; private init; }
        public List<ResourceUnit> ResourceUnits { get; private init; }
        public Dictionary<string, TreeSpeciesSet> SpeciesSetsByTableName { get; private init; }
        public GridRaster10m StandRaster { get; private init; } // retrieve the spatial grid that defines the stands (10m resolution)

        public float TotalStockableHectares { get; private set; } // total stockable area of the landscape (ha)

        public Landscape(Project projectFile)
        {
            if (String.IsNullOrWhiteSpace(projectFile.World.Initialization.ResourceUnitFile))
            {
                throw new NotSupportedException("Project file does not specify a resource unit file (model.world.initialization.resourceUnitFile).");
            }
            float worldBufferWidth = projectFile.World.Geometry.BufferWidth;
            if ((worldBufferWidth < Constant.HeightCellSizeInM) || (worldBufferWidth % Constant.HeightCellSizeInM != 0))
            {
                throw new NotSupportedException("World buffer width (model.world.geometry.bufferWidth) of " + projectFile.World.Geometry.BufferWidth + " m is not a positive, integer multiple of the height grid's cell size (" + Constant.HeightCellSizeInM + " m).");
            }

            this.WeatherByID = new();
            // this.Extent is set below
            this.GrassCover = new();
            // this.LightGrid is set below
            // this.HeightGrid is set below
            this.ResourceUnitGrid = new();
            this.ResourceUnits = new();
            this.SpeciesSetsByTableName = new();
            this.StandRaster = new();
            // this.TotalStockableHectares is set in this.CalculateStockableArea()

            // populate grids: resource units, height, and light (resource units have also a sapling grid)
            // The simulation area covered by the model is set to the minimum area encompassing all defined resource units as this
            // minimized the compute resources required. Some assumptions are made:
            //
            //  - If it's an area which needs to be modeled it's explicitly listed in the resource units file.
            //  - All input coordinates are in the same metric projected coordinate system. Therefore, they're consistent across all
            //    input files and all xy locations are in meters relative to the coordinate system's origin.
            //  - The resource unit grid is aligned with this projected coordinate system and is not rotated or warped.
            //
            // Input floating point coordinates in iLand are maintained in the coordinate system coming from GIS and are labeled as
            // GIS coordinates.
            //
            // iLand also uses integer indexes on its grids. Indexes are necessarily relative to the grids' origins (because that's
            // how processors index arrays) and the light and height grids extend a buffer width beyond the resource unit grid to
            // simplify height cell neighbor marking and tree light stamping. An iLand landscape therefore has two different grid
            // origins.
            //
            //  - The minimum coordinate of the light and height grids.
            //  - The minimum coordinate of resource units' bounding box for the resource unit grid, which is one world buffer width
            //    above the origin for the light and height grids.
            //
            // The minimum buffer width is one height cell but, in most cases, light stamping will require a
            // wider buffer be used. How wide of a buffer depends on tree stamp radii, which increase with tree size, but a 60 m buffer
            // is usually sufficient.
            //
            // Not uncommonly, floating point math is used to locate trees and grid cells in project coordinates. Similar to GIS
            // coordinates, these variables are named as such. In some cases iLand also works with resource unit coordinates, in which
            // case the origin is the resource unit coordinate with the minimum values (usually the southwest corner as this is the
            // position with the minimum easting and northing in most projected coordinate systems).
            ResourceUnitReader resourceUnitReader = new(projectFile);
            RectangleF resourceUnitExtent = resourceUnitReader.GetBoundingBox();
            this.ProjectOriginInGisCoordinates = new PointF(resourceUnitExtent.X - worldBufferWidth, resourceUnitExtent.Y - worldBufferWidth);
            this.ResourceUnitGrid.Setup(new RectangleF(worldBufferWidth, worldBufferWidth, resourceUnitExtent.Width, resourceUnitExtent.Height), Constant.ResourceUnitSizeInM);
            // resource units are created below, so grid contains only nulls at this point

            RectangleF bufferedExtent = new(0.0F, 0.0F, resourceUnitExtent.Width + 2 * worldBufferWidth, resourceUnitExtent.Height + 2 * worldBufferWidth);
            this.HeightGrid = new Grid<HeightCell>(bufferedExtent, Constant.HeightCellSizeInM);
            for (int index = 0; index < this.HeightGrid.CellCount; ++index)
            {
                this.HeightGrid[index] = new HeightCell();
            }

            this.LightGrid = new Grid<float>(bufferedExtent, Constant.LightCellSizeInM); // (re)initialized by Model at start of every timestep

            string? standRasterFile = projectFile.World.Initialization.StandRasterFile;
            if (String.IsNullOrEmpty(standRasterFile) == false)
            {
                string filePath = projectFile.GetFilePath(ProjectDirectory.Gis, standRasterFile);
                this.StandRaster.LoadFromFile(filePath);
                this.StandRaster.CreateIndex(this);

                for (int standIndex = 0; standIndex < this.StandRaster.Grid.CellCount; ++standIndex)
                {
                    int standID = this.StandRaster.Grid[standIndex];
                    this.HeightGrid[standIndex].SetOnLandscape(standID >= Constant.DefaultStandID);
                }
            }
            else
            {
                if (projectFile.World.Geometry.IsTorus == false)
                {
                    for (int heightGridIndex = 0; heightGridIndex < this.HeightGrid.CellCount; ++heightGridIndex)
                    {
                        PointF heightPosition = this.HeightGrid.GetCellProjectCentroid(heightGridIndex);
                        if ((heightPosition.X < 0.0F) || (heightPosition.Y < 0.0F) || (heightPosition.X > resourceUnitExtent.Width) || (heightPosition.Y > resourceUnitExtent.Width))
                        {
                            this.HeightGrid[heightGridIndex].SetOnLandscape(false);
                        }
                    }
                }
            }

            // instantiate resource units only where defined in resource unit file
            for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnitReader.Environments.Count; ++resourceUnitIndex)
            {
                ResourceUnitEnvironment environment = resourceUnitReader.Environments[resourceUnitIndex];

                if (this.WeatherByID.TryGetValue(environment.WeatherID, out WeatherDaily? weather) == false)
                {
                    // create only those climate sets that are really used in the current landscape
                    weather = new WeatherDaily(projectFile, environment.WeatherID);
                    this.WeatherByID.Add(environment.WeatherID, weather);
                }
                if (this.SpeciesSetsByTableName.TryGetValue(environment.SpeciesTableName, out TreeSpeciesSet? treeSpeciesSet) == false)
                {
                    treeSpeciesSet = new(environment.SpeciesTableName);
                    treeSpeciesSet.Setup(projectFile);
                    this.SpeciesSetsByTableName.Add(environment.SpeciesTableName, treeSpeciesSet);
                }

                // translate resource unit's position from GIS coordinates to project coordinates
                float ruProjectCentroidX = environment.GisCenterX - resourceUnitExtent.X + worldBufferWidth;
                float ruProjectCentroidY = environment.GisCenterY - resourceUnitExtent.Y + worldBufferWidth;
                Point ruGridIndexXY = this.ResourceUnitGrid.GetCellXYIndex(ruProjectCentroidX, ruProjectCentroidY);
                int ruGridIndex = this.ResourceUnitGrid.IndexXYToIndex(ruGridIndexXY);
                Debug.Assert((ruGridIndex >= 0) && (ruGridIndex < this.ResourceUnitGrid.CellCount));

                float ruMinProjectX = ruProjectCentroidX - 0.5F * Constant.ResourceUnitSizeInM;
                float ruMaxProjectY = ruProjectCentroidY + 0.5F * Constant.ResourceUnitSizeInM;
                float ruMinProjectY = ruProjectCentroidY - 0.5F * Constant.ResourceUnitSizeInM;
                ResourceUnit newRU = new(projectFile, weather, treeSpeciesSet, ruGridIndex)
                {
                    ProjectExtent = new RectangleF(ruMinProjectX, ruMinProjectY, Constant.ResourceUnitSizeInM, Constant.ResourceUnitSizeInM),
                    ID = environment.ResourceUnitID,
                    TopLeftLightIndexXY = this.LightGrid.GetCellXYIndex(ruMinProjectX, ruMaxProjectY)
                };
                newRU.Setup(projectFile, environment);
                this.ResourceUnits.Add(newRU);
                this.ResourceUnitGrid[ruGridIndex] = newRU; // save in the RUmap grid
            }
            if (this.ResourceUnits.Count == 0)
            {
                throw new NotSupportedException("Setup of Model: no resource units present!");
            }

            this.MarkHeightPixelsAndScaleSnags();

            if (projectFile.Model.Settings.RegenerationEnabled)
            {
                // mask off out of model areas in resource units' sapling grids
                GridWindowEnumerator<float> lightGridEnumerator = new(this.LightGrid, this.ResourceUnitGrid.ProjectExtent);
                while (lightGridEnumerator.MoveNext())
                {
                    SaplingCell? saplingCell = this.GetSaplingCell(this.LightGrid.GetCellXYIndex(lightGridEnumerator.CurrentIndex), false, out ResourceUnit _); // false: retrieve also invalid cells
                    if (saplingCell != null)
                    {
                        if (!this.HeightGrid[this.LightGrid.Index5(lightGridEnumerator.CurrentIndex)].IsOnLandscape())
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

            // setup of grass cover configuration
            // Initialization of grass cover values is done subsequently from GrassCover.SetInitialValues()
            this.GrassCover.Setup(projectFile, this);
        }

        /** calculate for each resource unit the stockable area.
          "stockability" is determined by the isValid flag of resource units which in turn
          is derived from stand grid values.
          */
        private void MarkHeightPixelsAndScaleSnags() // calculate the stockable area for each RU (i.e.: with stand grid values <> -1)
        {
            this.TotalStockableHectares = 0.0F;
            foreach (ResourceUnit ru in this.ResourceUnits)
            {
                //        if (ru.id()==-1) {
                //            ru.setStockableArea(0.);
                //            continue;
                //        }
                GridWindowEnumerator<HeightCell> ruHeightGridEnumerator = new(this.HeightGrid, ru.ProjectExtent);
                int heightCellsInLandscape = 0;
                int heightCellsInRU = 0;
                while (ruHeightGridEnumerator.MoveNext())
                {
                    HeightCell current = ruHeightGridEnumerator.Current;
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

                ru.AreaInLandscape = Constant.HeightCellAreaInM2 * heightCellsInLandscape; // in m²
                if (ru.Snags != null)
                {
                    ru.Snags.ScaleInitialState();
                }
                this.TotalStockableHectares += Constant.HeightCellAreaInM2 * heightCellsInLandscape / Constant.ResourceUnitAreaInM2; // in ha

                if (heightCellsInLandscape == 0 && ru.ID > -1)
                {
                    // invalidate this resource unit
                    // ru.ID = -1;
                    throw new NotSupportedException("Valid resource unit has no height cells in world.");
                }
                if (heightCellsInLandscape > 0 && ru.ID == -1)
                {
                    throw new NotSupportedException("Invalid resource unit " + ru.ResourceUnitGridIndex + " (" + ru.ProjectExtent + ") has height cells in world.");
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
            GridWindowEnumerator<HeightCell> heightGridEnumerator = new(this.HeightGrid, this.HeightGrid.ProjectExtent);
            HeightCell[] neighbors = new HeightCell[8];
            while (heightGridEnumerator.MoveNext())
            {
                if (heightGridEnumerator.Current.IsOnLandscape() == false)
                {
                    // if the current pixel is a "radiating" border pixel,
                    // then check the neighbors and set a flag if the pixel is a neighbor of a in-project-area pixel.
                    heightGridEnumerator.GetNeighbors8(neighbors);
                    for (int neighborIndex = 0; neighborIndex < neighbors.Length; ++neighborIndex)
                    {
                        if (neighbors[neighborIndex] != null && neighbors[neighborIndex].IsOnLandscape())
                        {
                            heightGridEnumerator.Current.SetIsRadiating();
                        }
                    }
                }
            }
        }

        private static int CompareLifValue((int Index, float LightValue) a, (int Index, float LightValue) b)
        {
            // reverse order
            if (a.LightValue > b.LightValue)
            {
                return -1;
            }
            if (a.LightValue < b.LightValue)
            {
                return 1;
            }
            return 0;
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

            SqliteConnectionStringBuilder connectionString = new()
            {
                DataSource = databaseFilePath,
                Mode = openReadOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            };
            SqliteConnection connection = new(connectionString.ConnectionString);
            connection.Open();
            if (openReadOnly == false)
            {
                // performance settings for output databases (http://www.sqlite.org/pragma.html)
                // Databases are typically expensive to create and maintain so SQLite defaults to conservative disk interactions. iLand
                // output data is cheap to generate and easy to recreate in the unlikely event something goes wrong flushing to disk, so
                // caution can be exchanged for speed. For example, journal_mode = memory, synchronous = off, and temp_store = memory 
                // make the model unit tests run 4-5x times faster than default settings.
                // pragma synchronous cannot be changed within a transaction
                using SqliteCommand synchronization = new("pragma synchronous(off)", connection);
                synchronization.ExecuteNonQuery();

                using SqliteTransaction transaction = connection.BeginTransaction();
                // little to no difference between journal_mode = memory and journal_mode = off
                using SqliteCommand journalMode = new("pragma journal_mode(memory)", connection, transaction);
                journalMode.ExecuteNonQuery();
                using SqliteCommand tempStore = new("pragma temp_store(memory)", connection, transaction);
                tempStore.ExecuteNonQuery();
                transaction.Commit();
            }

            return connection;
        }

        public ResourceUnit GetResourceUnit(PointF projectCoordinate) // resource unit at given project coordinates
        {
            float resourceUnitGridCoordinateX = projectCoordinate.X - this.ResourceUnitGrid.ProjectExtent.X;
            float resourceUnitGridCoordinateY = projectCoordinate.Y - this.ResourceUnitGrid.ProjectExtent.Y;
            ResourceUnit? ru = this.ResourceUnitGrid[resourceUnitGridCoordinateX, resourceUnitGridCoordinateY];
            if (ru == null)
            {
                throw new ArgumentOutOfRangeException(nameof(projectCoordinate));
            }
            return ru;
        }

        /// return the SaplingCell (i.e. container for the ind. saplings) for the given 2x2m coordinates
        /// if 'only_valid' is true, then null is returned if no living saplings are on the cell
        /// 'rRUPtr' is a pointer to a RU-ptr: if provided, a pointer to the resource unit is stored
        public SaplingCell? GetSaplingCell(Point lightCellPosition, bool onlyValid, out ResourceUnit ru)
        {
            ru = this.GetResourceUnit(this.LightGrid.GetCellProjectCentroid(lightCellPosition));
            SaplingCell saplingCell = ru.GetSaplingCell(lightCellPosition);
            if ((saplingCell != null) && (!onlyValid || saplingCell.State != SaplingCellState.Invalid))
            {
                return saplingCell;
            }
            return null;
        }

        private void SetupSaplingsAndGrass(int standID, List<StandSaplings> saplingsInStands, int standStartIndex, int standEndIndex, RandomGenerator randomGenerator)
        {
            GridRaster10m? standGrid = this.StandRaster; // default
            if (standGrid == null)
            {
                throw new NotSupportedException();
            }
            if (standGrid.IsIndexed(standID) == false)
            {
                throw new NotSupportedException();
            }

            List<int> standGridIndices = standGrid.GetGridIndices(standID); // list of 10x10m pixels
            if (standGridIndices.Count == 0)
            {
                // if (projectFile.Output.Logging.LogLevel >= EventLevel.Informational)
                // {
                //     Trace.TraceInformation("Stand " + standID + " not in project area. No initialization performed.");
                // }
                return;
            }

            // prepare space for LIF-pointers (2m cells)
            List<(int CellIndex, float LightValue)> lightCellIndicesAndValues = new(standGridIndices.Count * Constant.LightCellsPerHeightCellWidth * Constant.LightCellsPerHeightCellWidth);
            Grid<float> lightGrid = this.LightGrid;
            for (int standGridIndex = 0; standGridIndex < standGridIndices.Count; ++standGridIndex)
            {
                Point cellOrigin = standGrid.Grid.GetCellXYIndex(standGridIndices[standGridIndex]);
                cellOrigin.X *= Constant.LightCellsPerHeightCellWidth; // index of 10m patch -> to lif pixel coordinates
                cellOrigin.Y *= Constant.LightCellsPerHeightCellWidth;
                for (int lightY = 0; lightY < Constant.LightCellsPerHeightCellWidth; ++lightY)
                {
                    for (int lightX = 0; lightX < Constant.LightCellsPerHeightCellWidth; ++lightX)
                    {
                        int modelIndex = lightGrid.IndexXYToIndex(cellOrigin.X + lightX, cellOrigin.Y + lightY);
                        (int, float) indexAndValue = new(modelIndex, lightGrid[modelIndex]);
                        lightCellIndicesAndValues.Add(indexAndValue);
                    }
                }
            }
            // sort based on LIF-Value
            lightCellIndicesAndValues.Sort(Landscape.CompareLifValue); // higher: highest values first

            float standAreaInHa = standGrid.GetAreaInSquareMeters(standID) / Constant.ResourceUnitAreaInM2; // multiplier for grid (e.g. 2 if stand has area of 2 hectare)

            int grassCoverPercentage = -1;
            for (int rowIndexInStand = standStartIndex; rowIndexInStand <= standEndIndex; ++rowIndexInStand)
            {
                StandSaplings saplingsInStand = saplingsInStands[rowIndexInStand];
                int cellsWithSaplings = (int)(saplingsInStand.Count * standAreaInHa); // number of sapling grid cells that should be filled (sapling grid is the same resolution as the light grid)

                // find LIF-level in the pixels
                int minLightIndex = 0;
                if (saplingsInStand.MinLightIntensity < 1.0)
                {
                    for (int lightIndex = 0; lightIndex < lightCellIndicesAndValues.Count; ++lightIndex, ++minLightIndex)
                    {
                        if (lightCellIndicesAndValues[lightIndex].LightValue <= saplingsInStand.MinLightIntensity)
                        {
                            break;
                        }
                    }
                    if (cellsWithSaplings < minLightIndex)
                    {
                        // not enough light grid cells available
                        minLightIndex = cellsWithSaplings; // try the brightest pixels (ie with the largest value for the LIF)
                    }
                }
                else
                {
                    // No LIF threshold: the full range of pixels is valid
                    minLightIndex = lightCellIndicesAndValues.Count;
                }

                float fractionallyOccupiedCellCount = 0.0F;
                while (fractionallyOccupiedCellCount < cellsWithSaplings)
                {
                    int randomIndex = randomGenerator.GetRandomInteger(0, minLightIndex);

                    int age = saplingsInStand.Age;
                    float height = saplingsInStand.Height;
                    if (Single.IsNaN(height))
                    {
                        height = (float)randomGenerator.GetRandomFloat(saplingsInStand.MinHeight, saplingsInStand.MaxHeight);
                        if (age <= 1)
                        {
                            // assume a linear relationship between height and age
                            age = Math.Max((int)MathF.Round(height / Constant.Sapling.MaximumHeight * saplingsInStand.AgeAt4m), 1);
                        }
                    }

                    Point lightCellIndex = lightGrid.GetCellXYIndex(lightCellIndicesAndValues[randomIndex].CellIndex);
                    SaplingCell? saplingCell = this.GetSaplingCell(lightCellIndex, true, out ResourceUnit ru);
                    if (saplingCell != null)
                    {
                        TreeSpecies species = ru.Trees.TreeSpeciesSet[saplingsInStand.Species];
                        Sapling? sapling = saplingCell.AddSaplingIfSlotFree(height, age, species.Index);
                        if (sapling != null)
                        {
                            fractionallyOccupiedCellCount += Math.Max(1.0F, species.SaplingGrowthParameters.RepresentedStemNumberFromHeight(sapling.HeightInM));
                        }
                        else
                        {
                            ++fractionallyOccupiedCellCount;
                        }
                    }
                    else
                    {
                        ++fractionallyOccupiedCellCount; // avoid an infinite loop
                    }
                }

                int grassCoverPercentageInStand = saplingsInStand.GrassCoverPercentage;
                if (grassCoverPercentageInStand > 0)
                {
                    if ((grassCoverPercentage != -1) && (grassCoverPercentage != grassCoverPercentageInStand))
                    {
                        throw new NotSupportedException("The grass cover percentage (" + grassCoverPercentageInStand + "%) for stand '" + standID + "' differs from the cover percentage in the sapling file's earlier rows for the stand.");
                    }
                    else
                    {
                        grassCoverPercentage = grassCoverPercentageInStand;
                    }
                }
            }

            // initialize grass cover
            if (grassCoverPercentage > -1)
            {
                this.GrassCover.SetInitialValues(randomGenerator, lightCellIndicesAndValues, grassCoverPercentage);
            }
        }

        public void SetupSaplingsAndGrass(Project projectFile, RandomGenerator randomGenerator)
        {
            // nothing more to do if no saplings are specified
            string? saplingFileName = projectFile.World.Initialization.SaplingsByStandFile;
            if (String.IsNullOrEmpty(saplingFileName))
            {
                return;
            }

            // load a file with saplings per stand
            string saplingFilePath = projectFile.GetFilePath(ProjectDirectory.Init, saplingFileName);
            using CsvFile saplingFile = new(saplingFilePath);
            StandSaplingsDataIndex saplingHeader = new(saplingFile);

            // TODO: should this be sorted by stand ID?
            List<StandSaplings> saplingsInStands = new();
            saplingFile.Parse((string[] row) =>
            {
                StandSaplings saplings = new(saplingHeader, row);
                saplingsInStands.Add(saplings);
            });

            if (this.StandRaster.IsSetup())
            {
                int previousStandID = Constant.NoDataInt32;
                int standStartIndex = -1;
                for (int standIndex = 0; standIndex < saplingsInStands.Count; ++standIndex)
                {
                    StandSaplings saplings = saplingsInStands[standIndex];
                    if (saplings.StandID != previousStandID)
                    {
                        if (previousStandID >= Constant.DefaultStandID)
                        {
                            // process stand
                            int standEndIndex = standIndex - 1; // up to the last
                            this.SetupSaplingsAndGrass(previousStandID, saplingsInStands, standStartIndex, standEndIndex, randomGenerator);
                        }
                        standStartIndex = standIndex; // mark beginning of new stand
                        previousStandID = saplings.StandID;
                    }
                }
                if (previousStandID >= Constant.DefaultStandID)
                {
                    this.SetupSaplingsAndGrass(previousStandID, saplingsInStands, standStartIndex, saplingsInStands.Count - 1, randomGenerator); // the last stand
                }
            }
            else if (saplingsInStands.Count != 0)
            {
                throw new NotSupportedException("Sapling initialization not supported unless a stand raster is provided.");
            }
            // nothing to do: sapling file is empty
        }
    }
}
