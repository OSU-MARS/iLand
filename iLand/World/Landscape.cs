using iLand.Input;
using iLand.Input.ProjectFile;
using iLand.Input.Tree;
using iLand.Input.Weather;
using iLand.Tool;
using iLand.Tree;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace iLand.World
{
    public class Landscape
    {
        public CO2TimeSeriesMonthly CO2ByMonth { get; private init; }
        public GrassCover GrassCover { get; private init; }
        public Grid<float> LightGrid { get; private init; } // this is the global 'LIF'-grid (light patterns) (currently 2x2m)
        public PointF ProjectOriginInGisCoordinates { get; private init; }

        public Grid<ResourceUnit?> ResourceUnitGrid { get; private init; }
        public List<ResourceUnit> ResourceUnits { get; private init; }
        public Dictionary<string, TreeSpeciesSet> SpeciesSetsByTableName { get; private init; }
        public GridRaster10m StandRaster { get; private init; } // retrieve the spatial grid that defines the stands (10m resolution)

        public Grid<float> VegetationHeightGrid { get; private init; } // stores maximum height of vegetation in m, currently 10 x 10 m cells
        public Dictionary<string, Weather> WeatherByID { get; private init; }
        public int WeatherFirstCalendarYear { get; private init; }

        public Landscape(Project projectFile, ParallelOptions parallelComputeOptions)
        {
            if (String.IsNullOrWhiteSpace(projectFile.World.Initialization.ResourceUnitFile))
            {
                throw new NotSupportedException("Project file does not specify a resource unit file (/project/model/world/initialization/resourceUnitFile).");
            }
            float worldBufferWidth = projectFile.World.Geometry.BufferWidthInM;
            if ((worldBufferWidth < Constant.HeightCellSizeInM) || (worldBufferWidth % Constant.HeightCellSizeInM != 0))
            {
                throw new NotSupportedException("World buffer width (/project/model/world/geometry/bufferWidth) of " + projectFile.World.Geometry.BufferWidthInM + " m is not a positive, integer multiple of the height grid's cell size (" + Constant.HeightCellSizeInM + " m).");
            }

            // if available, read monthly weather data in parallel with resource unit setup
            string weatherFilePath = projectFile.GetFilePath(ProjectDirectory.Database, projectFile.World.Weather.WeatherFile);
            string? weatherFileExtension = Path.GetExtension(weatherFilePath);
            Func<WeatherReaderMonthly>? readMonthlyWeatherFromFile = weatherFileExtension switch
            {
                // for now, assume .csv and .feather weather is monthly and all weather tables in SQLite databases are daily
                Constant.File.CsvExtension => () => new WeatherReaderMonthlyCsv(weatherFilePath, projectFile.World.Weather.StartYear),
                Constant.File.FeatherExtension => () => new WeatherReaderMonthlyFeather(weatherFilePath, projectFile.World.Weather.StartYear),
                Constant.File.SqliteExtension => null,
                _ => throw new NotSupportedException("Unhandled weather file extension '" + weatherFileExtension + "'.")
            };

            Task<WeatherReaderMonthly>? readMonthlyWeather = readMonthlyWeatherFromFile != null ? Task.Run(readMonthlyWeatherFromFile) : null;

            // read monthly CO₂ in parallel with resource unit setup
            string co2filePath = projectFile.GetFilePath(ProjectDirectory.Database, projectFile.World.Weather.CO2File);
            string? co2fileExtension = Path.GetExtension(projectFile.World.Weather.CO2File);
            CO2ReaderMonthly monthlyCO2reader = co2fileExtension switch
            {
                Constant.File.CsvExtension => new CO2ReaderMonthlyCsv(co2filePath, Constant.Data.DefaultMonthlyAllocationIncrement),
                Constant.File.FeatherExtension => new CO2ReaderMonthlyFeather(co2filePath, Constant.Data.DefaultMonthlyAllocationIncrement),
                _ => throw new NotSupportedException("Unhandled CO₂ file extension '" + co2fileExtension + "'.")
            };

            this.CO2ByMonth = monthlyCO2reader.TimeSeries;
            // this.Extent is set below
            this.GrassCover = new();
            // this.LightGrid is set below
            // this.HeightGrid is set below
            this.ResourceUnitGrid = new();
            this.ResourceUnits = new();
            this.SpeciesSetsByTableName = new();
            this.StandRaster = new();
            // this.TotalStockableHectares is set in this.CalculateStockableArea()
            this.WeatherByID = new();
            this.WeatherFirstCalendarYear = Constant.NoDataInt32;

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
            ResourceUnitEnvironment defaultEnvironment = new(projectFile.World);
            string resourceUnitFilePath = projectFile.GetFilePath(ProjectDirectory.Gis, projectFile.World.Initialization.ResourceUnitFile);
            string? resourceUnitExtension = Path.GetExtension(resourceUnitFilePath);
            ResourceUnitReader resourceUnitReader = resourceUnitExtension switch
            {
                // for now, assume .csv and .feather weather is monthly and all weather tables in SQLite databases are daily
                Constant.File.CsvExtension => new ResourceUnitReaderCsv(resourceUnitFilePath, defaultEnvironment),
                Constant.File.FeatherExtension => new ResourceUnitReaderFeather(resourceUnitFilePath, defaultEnvironment),
                _ => throw new NotSupportedException("Unhandled resource unit environment file extension '" + resourceUnitExtension + "'.")
            };
            RectangleF resourceUnitGisExtent = resourceUnitReader.GetBoundingBox();
            this.ProjectOriginInGisCoordinates = new(resourceUnitGisExtent.X - worldBufferWidth, resourceUnitGisExtent.Y - worldBufferWidth);
            this.ResourceUnitGrid.Setup(new RectangleF(worldBufferWidth, worldBufferWidth, resourceUnitGisExtent.Width, resourceUnitGisExtent.Height), Constant.ResourceUnitSizeInM);
            // resource units are created below, so grid contains only nulls at this point

            RectangleF bufferedExtent = new(0.0F, 0.0F, resourceUnitGisExtent.Width + 2 * worldBufferWidth, resourceUnitGisExtent.Height + 2 * worldBufferWidth);
            this.LightGrid = new(bufferedExtent, Constant.LightCellSizeInM); // (re)initialized by Model at start of every timestep
            this.VegetationHeightGrid = new(bufferedExtent, Constant.HeightCellSizeInM);

            // instantiate resource units only where defined in resource unit file
            WeatherReaderMonthly? monthlyWeatherReader = readMonthlyWeather?.GetAwaiter().GetResult();
            for (int resourceUnitIndex = 0; resourceUnitIndex < resourceUnitReader.Count; ++resourceUnitIndex)
            {
                ResourceUnitEnvironment environment = resourceUnitReader.Environments[resourceUnitIndex];

                string weatherID = environment.WeatherID;
                if (this.WeatherByID.TryGetValue(weatherID, out Weather? weather) == false)
                {
                    // create only those climate sets that are really used in the current landscape
                    if (monthlyWeatherReader != null)
                    {
                        weather = new WeatherMonthly(projectFile, monthlyWeatherReader.MonthlyWeatherByID[weatherID]);
                    }
                    else
                    {
                        weather = new WeatherDaily(weatherFilePath, weatherID, projectFile);
                    }
                    this.WeatherByID.Add(weatherID, weather);

                    int firstCalendarYearInWeatherTimeSeries = weather.TimeSeries.Year[0];
                    if (this.WeatherFirstCalendarYear == Constant.NoDataInt32)
                    {
                        this.WeatherFirstCalendarYear = firstCalendarYearInWeatherTimeSeries;
                    }
                    else if (this.WeatherFirstCalendarYear != firstCalendarYearInWeatherTimeSeries)
                    {
                        throw new NotSupportedException("Weather time series '" + weatherID + "' begins in calendar year " + firstCalendarYearInWeatherTimeSeries + ", which does not match other weather time series beginning in " + this.WeatherFirstCalendarYear + ".");
                    }
                }
                if (this.SpeciesSetsByTableName.TryGetValue(environment.SpeciesTableName, out TreeSpeciesSet? treeSpeciesSet) == false)
                {
                    treeSpeciesSet = new(projectFile, environment.SpeciesTableName);
                    this.SpeciesSetsByTableName.Add(environment.SpeciesTableName, treeSpeciesSet);
                }

                // translate resource unit's position from GIS coordinates to project coordinates
                float ruProjectCentroidX = environment.GisCenterX - this.ProjectOriginInGisCoordinates.X;
                float ruProjectCentroidY = environment.GisCenterY - this.ProjectOriginInGisCoordinates.Y;
                Point ruGridIndexXY = this.ResourceUnitGrid.GetCellXYIndex(ruProjectCentroidX, ruProjectCentroidY);
                int ruGridIndex = this.ResourceUnitGrid.IndexXYToIndex(ruGridIndexXY);
                Debug.Assert((ruGridIndex >= 0) && (ruGridIndex < this.ResourceUnitGrid.CellCount));

                float ruMinProjectX = ruProjectCentroidX - 0.5F * Constant.ResourceUnitSizeInM;
                float ruMinProjectY = ruProjectCentroidY - 0.5F * Constant.ResourceUnitSizeInM;
                ResourceUnit newRU = new(projectFile, weather, treeSpeciesSet, ruGridIndex)
                {
                    ProjectExtent = new RectangleF(ruMinProjectX, ruMinProjectY, Constant.ResourceUnitSizeInM, Constant.ResourceUnitSizeInM),
                    ID = environment.ResourceUnitID,
                    MinimumLightIndexXY = this.LightGrid.GetCellXYIndex(ruMinProjectX, ruMinProjectY)
                };
                newRU.SetupEnvironment(projectFile, environment);
                this.ResourceUnits.Add(newRU);
                this.ResourceUnitGrid[ruGridIndex] = newRU; // save in the RUmap grid
            }
            if (this.ResourceUnits.Count == 0)
            {
                throw new NotSupportedException("No resource units present!");
            }

            // mark height cells as on landscape (in a resource unit or stand) or leave height flags as default (off landscape)
            // On landscape marking is required before marking edge height cells as radiating because, otherwise, there no on-landscape,
            // off-landscape edges exist to detect.
            string? standRasterFile = projectFile.World.Initialization.StandRasterFile;
            if (String.IsNullOrEmpty(standRasterFile) == false)
            {
                string filePath = projectFile.GetFilePath(ProjectDirectory.Gis, standRasterFile);
                this.StandRaster.LoadFromFile(filePath);
                if ((this.StandRaster.Grid.CellCount != this.VegetationHeightGrid.CellCount) || (this.StandRaster.Grid.ProjectExtent != this.VegetationHeightGrid.ProjectExtent))
                {
                    throw new NotSupportedException("Extent of stand raster does not match extent of vegetation height grid.");
                }
                this.StandRaster.CreateIndex(this);

                throw new NotSupportedException("Stand raster is not currently applied to resource units' height cells.");
            }

            this.MarkSaplingCellsAndScaleSnags(parallelComputeOptions);

            // setup of grass cover configuration
            // Initialization of grass cover values is done subsequently from GrassCover.SetInitialValues()
            this.GrassCover.Setup(projectFile, this);
        }

        /** calculate for each resource unit the stockable area.
          "stockability" is determined by the isValid flag of resource units which in turn
          is derived from stand grid values.
          */
        private void MarkSaplingCellsAndScaleSnags(ParallelOptions parallelComputeOptions)
        {
            Parallel.For(0, this.ResourceUnits.Count, parallelComputeOptions, (int resourceUnitIndex) =>
            {
                ResourceUnit resourceUnit = this.ResourceUnits[resourceUnitIndex];
                // calculate the stockable area for each RU (i.e.: with stand grid values <> -1)
                // for now, all height cells in a resource unit are on landscape
                int heightCellsInLandscape = Constant.HeightCellsPerRUWidth * Constant.HeightCellsPerRUWidth;
                resourceUnit.AreaInLandscapeInM2 = Constant.HeightCellAreaInM2 * heightCellsInLandscape;
                resourceUnit.HeightCellsOnLandscape = heightCellsInLandscape;

                if (resourceUnit.SaplingCells != null)
                {
                    // for now, all sapling cells in resource unit are available for germination
                    for (int saplingCellIndex = 0; saplingCellIndex < resourceUnit.SaplingCells.Length; ++saplingCellIndex)
                    {
                        SaplingCell saplingCell = resourceUnit.SaplingCells[saplingCellIndex];
                        saplingCell.State = SaplingCellState.Free;
                    }
                }
                if (resourceUnit.Snags != null)
                {
                    resourceUnit.Snags.ScaleInitialState();
                }
            });
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
            Point resourceUnitIndexXY = this.ResourceUnitGrid.GetCellXYIndex(projectCoordinate);
            ResourceUnit? resourceUnit = this.ResourceUnitGrid[resourceUnitIndexXY.X, resourceUnitIndexXY.Y];
            if (resourceUnit == null)
            {
                throw new ArgumentOutOfRangeException(nameof(projectCoordinate), "No resource unit is present at project coordinate x = " + projectCoordinate.X + ", y = " + projectCoordinate.Y + " m.");
            }
            return resourceUnit;
        }

        /// return the SaplingCell (i.e. container for the ind. saplings) for the given 2x2m coordinates
        /// if 'only_valid' is true, then null is returned if no living saplings are on the cell
        /// 'rRUPtr' is a pointer to a RU-ptr: if provided, a pointer to the resource unit is stored
        public SaplingCell? GetSaplingCell(Point lightCellPosition, bool onlyValid, out ResourceUnit resourceUnit)
        {
            resourceUnit = this.GetResourceUnit(this.LightGrid.GetCellProjectCentroid(lightCellPosition));
            SaplingCell saplingCell = resourceUnit.GetSaplingCell(lightCellPosition);
            if ((saplingCell != null) && (!onlyValid || saplingCell.State != SaplingCellState.NotOnLandscape))
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
                    SaplingCell? saplingCell = this.GetSaplingCell(lightCellIndex, true, out ResourceUnit resourceUnit);
                    if (saplingCell != null)
                    {
                        TreeSpecies species = resourceUnit.Trees.TreeSpeciesSet[saplingsInStand.SpeciesID];
                        Sapling? sapling = saplingCell.AddSaplingIfSlotFree(height, age, species.Index);
                        if (sapling != null)
                        {
                            fractionallyOccupiedCellCount += MathF.Max(1.0F, species.SaplingGrowth.RepresentedStemNumberFromHeight(sapling.HeightInM));
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
            StandSaplingsCsvHeader saplingHeader = new(saplingFile);

            // TODO: should this be sorted by stand ID?
            List<StandSaplings> saplingsInStands = new();
            saplingFile.Parse((SplitString row) =>
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
                        // process stand
                        int standEndIndex = standIndex - 1; // up to the last
                        this.SetupSaplingsAndGrass(previousStandID, saplingsInStands, standStartIndex, standEndIndex, randomGenerator);

                        standStartIndex = standIndex; // mark beginning of new stand
                        previousStandID = saplings.StandID;
                    }
                }
                this.SetupSaplingsAndGrass(previousStandID, saplingsInStands, standStartIndex, saplingsInStands.Count - 1, randomGenerator); // the last stand
            }
            else if (saplingsInStands.Count != 0)
            {
                throw new NotSupportedException("Sapling initialization not supported unless a stand raster is provided.");
            }
            // nothing to do: sapling file is empty
        }
    }
}
