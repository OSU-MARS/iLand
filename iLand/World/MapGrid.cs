using iLand.Simulation;
using iLand.Tool;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.World
{
    /** MapGrid encapsulates maps that classify the area in 10m resolution (e.g. for stand-types, management-plans, ...)
      The grid is (currently) loaded from disk in a ESRI style text file format. See also the "location" keys and GisTransformation classes for
      details on how the grid is mapped to the local coordinate system of the project area. From the source grid a 10m grid
      using the extent and position of the "Grid<HeightGridValue>" and spatial indices for faster access are generated.
      The grid is clipped to the extent of the simulation area and -1 is used for no_data_values.
      Use boundingBox(), resourceUnits(), trees() to retrieve information for specific 'ids'. gridValue() retrieves the 'id' for a given
      location (in LIF-coordinates).
      */
    public class MapGrid
    {
        private readonly Dictionary<int, MutableTuple<RectangleF, float>> mBoundingBoxByStandID; // holds the extent and area for each map-id
        private readonly Dictionary<int, List<MutableTuple<ResourceUnit, float>>> mResourceUnitsByStandID; // holds a list of resource units + areas per map-id
        private readonly Dictionary<int, List<int>> mNeighborListByStandID; // a list of neighboring polygons; for each ID all neighboring IDs are stored.

        public string? FileName { get; private set; }
        public Grid<int> Grid { get; private init; }

        public MapGrid()
        {
            this.mBoundingBoxByStandID = new Dictionary<int, MutableTuple<RectangleF, float>>();
            this.mNeighborListByStandID = new Dictionary<int, List<int>>();
            this.mResourceUnitsByStandID = new Dictionary<int, List<MutableTuple<ResourceUnit, float>>>();

            this.FileName = null;
            this.Grid = new Grid<int>();
        }

        public MapGrid(Landscape landscape, string? fileName)
            : this()
        {
            this.LoadFromFile(landscape, fileName);
        }

        public float GetArea(int standID) { return this.IsValid(standID) ? mBoundingBoxByStandID[standID].Item2 : 0.0F; } // return the area (m2) covered by the polygon
        public RectangleF GetBoundingBox(int id) { return this.IsValid(id) ? mBoundingBoxByStandID[id].Item1 : new RectangleF(); } // returns the bounding box of a polygon
        public bool IsValid() { return !this.Grid.IsNotSetup(); }
        /// returns true, if 'id' is a valid id in the grid, false otherwise.
        public bool IsValid(int standID) { return mBoundingBoxByStandID.ContainsKey(standID); }

        /// return true, if the point 'lif_grid_coords' (x/y integer key within the LIF-Grid)
        public bool HasValue(int id, Point lightCoordinate)
        {
            return this.GetStandIDFromLightCoordinate(lightCoordinate) == id;
        }

        /// return the stand-ID at the coordinates *from* the LIF-Grid (i.e., 2m grid).
        public int GetStandIDFromLightCoordinate(Point lightCoordinate)
        {
            return this.Grid[lightCoordinate.X, lightCoordinate.Y, Constant.LightCellsPerHeightSize];
        }

        // load from an already present GisGrid
        public bool LoadFromGrid(Landscape landscape, GisGrid sourceGrid, bool createIndex = true)
        {
            if ((landscape == null) || (landscape.HeightGrid == null) || landscape.HeightGrid.IsNotSetup())
            {
                throw new ArgumentNullException(nameof(landscape), "No height grid available.");
            }

            Grid<HeightCell> heightGrid = landscape.HeightGrid;
            // create a grid with the same size as the height grid
            // (height-grid: 10m size, covering the full extent)
            this.Grid.Clear();
            this.Grid.Setup(heightGrid.PhysicalExtent, heightGrid.CellSize);

            for (int gridIndex = 0; gridIndex < this.Grid.Count; gridIndex++)
            {
                PointF centerPoint = this.Grid.GetCellCenterPosition(this.Grid.GetCellPosition(gridIndex));
                if (sourceGrid.GetValue(centerPoint) != sourceGrid.NoDataValue && landscape.Extent.Contains(centerPoint))
                {
                    this.Grid[gridIndex] = (int)sourceGrid.GetValue(centerPoint);
                }
                else
                {
                    this.Grid[gridIndex] = -1;
                }
            }

            // create spatial index
            mBoundingBoxByStandID.Clear();
            mResourceUnitsByStandID.Clear();

            if (createIndex)
            {
                this.CreateIndex(landscape);
            }
            return true;
        }

        public void CreateEmptyGrid(Landscape landscape)
        {
            Grid<HeightCell> heightGrid = landscape.HeightGrid;
            if (heightGrid == null || heightGrid.IsNotSetup())
            {
                throw new NotSupportedException("No valid height grid from which to copy grid size.");
            }
            // create a grid with the same size as the height grid
            // (height-grid: 10m size, covering the full extent)
            this.Grid.Clear();
            this.Grid.Setup(heightGrid.PhysicalExtent, heightGrid.CellSize);
            this.Grid.Fill(0);

            // reset spatial index
            this.mBoundingBoxByStandID.Clear();
            this.mResourceUnitsByStandID.Clear();
        }

        public void CreateIndex(Landscape landscape)
        {
            // reset spatial index
            this.mBoundingBoxByStandID.Clear();
            this.mResourceUnitsByStandID.Clear();
            // create new
            for (int gridIndex = 0; gridIndex < this.Grid.Count; ++gridIndex)
            {
                if (this.Grid[gridIndex] == -1)
                {
                    continue;
                }
                MutableTuple<RectangleF, float> data = this.mBoundingBoxByStandID[gridIndex];
                data.Item1 = RectangleF.Union(data.Item1, this.Grid.GetCellExtent(this.Grid.GetCellPosition(gridIndex)));
                data.Item2 += Constant.LightSize * Constant.LightCellsPerHeightSize * Constant.LightSize * Constant.LightCellsPerHeightSize; // 100m2

                ResourceUnit ru = landscape.GetResourceUnit(this.Grid.GetCellCenterPosition(this.Grid.GetCellPosition(gridIndex)));
                if (ru == null)
                {
                    continue;
                }
                // find all entries for the current grid id
                // TODO: why is lookup by grid index rather than stand ID?
                List<MutableTuple<ResourceUnit, float>> resourceUnitsInStand = mResourceUnitsByStandID[gridIndex];

                // look for the resource unit 'ru'
                bool ruFound = false;
                Debug.Assert(Constant.HeightSizePerRU * Constant.HeightSizePerRU == 100); // 100 height cells per RU -> 1% RU area per height cell
                Debug.Assert(this.Grid.CellSize == Constant.HeightSizePerRU);
                for (int index = 0; index < resourceUnitsInStand.Count; ++index)
                {
                    MutableTuple<ResourceUnit, float> candidate = resourceUnitsInStand[index];
                    if (candidate.Item1 == ru)
                    {
                        candidate.Item2 += 0.01F; // 1 pixel = 1% of the area
                        ruFound = true;
                        break;
                    }
                }
                if (ruFound == false)
                {
                    mResourceUnitsByStandID.AddToList(gridIndex, new MutableTuple<ResourceUnit, float>(ru, 0.01F)); // TODO: why add non-intersecting RUs with 0.01 instead of 0.0?
                }
            }
        }

        // load ESRI style text file
        public bool LoadFromFile(Landscape landscape, string? fileName)
        {
            GisGrid gisGrid = new();
            if (gisGrid.LoadFromFile(fileName))
            {
                this.FileName = fileName;
                return this.LoadFromGrid(landscape, gisGrid, createIndex: false);
            }
            else
            {
                this.FileName = "invalid";
            }
            return false;
        }

        /// returns a list with resource units and area factors per 'id'.
        /// the area is '1' if the resource unit is fully covered by the grid-value.
        public IList<MutableTuple<ResourceUnit, float>> GetResourceUnitAreaFractions(int standID)
        {
            return this.mResourceUnitsByStandID[standID]; 
        }

        /// returns the list of resource units with at least one pixel within the area designated by 'id'
        public List<ResourceUnit> GetResourceUnitsInStand(int standID)
        {
            IReadOnlyCollection<MutableTuple<ResourceUnit, float>> resourceUnitsInStand = mResourceUnitsByStandID[standID];
            List<ResourceUnit> resourceUnits = new(resourceUnitsInStand.Count);
            foreach (MutableTuple<ResourceUnit, float> ru in resourceUnitsInStand)
            {
                resourceUnits.Add(ru.Item1);
            }
            return resourceUnits;
        }

        /// return a list of all living trees on the area denoted by 'id'
        public List<MutableTuple<Trees, List<int>>> GetLivingTreesInStand(int standID)
        {
            List<MutableTuple<Trees, List<int>>> livingTrees = new();
            List<ResourceUnit> resourceUnitsInStand = this.GetResourceUnitsInStand(standID);
            foreach (ResourceUnit ru in resourceUnitsInStand)
            {
                foreach (Trees trees in ru.Trees.TreesBySpeciesID.Values)
                {
                    MutableTuple<Trees, List<int>> livingTreesInStand = new(trees, new List<int>());
                    for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                    {
                        if ((this.GetStandIDFromLightCoordinate(trees.LightCellPosition[treeIndex]) == standID) && (trees.IsDead(treeIndex) == false))
                        {
                            livingTreesInStand.Item2.Add(treeIndex);
                        }
                    }

                    livingTrees.Add(livingTreesInStand);
                }
            }
            //    qDebug() << "trees: found" << c << "/" << tree_list.size();
            return livingTrees;
        }

        public int LoadTrees(Model model, int id, List<MutableTuple<Tree.Trees, float>> rList, string filter, int estimatedTreeCount)
        {
            rList.Clear();
            if (estimatedTreeCount > 0)
            {
                rList.Capacity = estimatedTreeCount;
            }
            Expression? treeFilterExpression = null;
            TreeWrapper treeWrapper = new(model);
            if (String.IsNullOrEmpty(filter) == false)
            {
                treeFilterExpression = new Expression(filter, treeWrapper);
                treeFilterExpression.EnableIncrementalSum();
            }
            // lock the resource units: removed again, WR20140821
            // mapGridLock.lock(id, resource_units);

            List<ResourceUnit> resourceUnitsInStand = GetResourceUnitsInStand(id);
            foreach (ResourceUnit ru in resourceUnitsInStand)
            {
                foreach (Trees treesOfSpecies in ru.Trees.TreesBySpeciesID.Values)
                {
                    treeWrapper.Trees = treesOfSpecies;
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        if ((this.GetStandIDFromLightCoordinate(treesOfSpecies.LightCellPosition[treeIndex]) == id) && (treesOfSpecies.IsDead(treeIndex) == false))
                        {
                            if (treeFilterExpression != null)
                            {
                                treeWrapper.TreeIndex = treeIndex;
                                double value = treeFilterExpression.Evaluate(treeWrapper);
                                // keep if expression returns true (1)
                                bool loadTree = value == 1.0;
                                // if value is >0 (i.e. not "false"), then draw a random number
                                if ((loadTree == false) && (value > 0.0))
                                {
                                    loadTree = model.RandomGenerator.GetRandomFloat() < value;
                                }
                                if (loadTree == false)
                                {
                                    continue;
                                }
                            }
                            rList.Add(new MutableTuple<Tree.Trees, float>(treesOfSpecies, 0.0F));
                        }
                    }
                }
            }
            return rList.Count;
        }

        //public void FreeLocksForStand(int id)
        //{
        //    if (id > -1)
        //    {
        //        mapGridLock.Unlock(id);
        //    }
        //}

        /// return a list of grid-indices of a given stand-id (a grid-index
        /// is the index of 10m x 10m pixels within the internal storage)
        /// The selection is limited to pixels within the world's extent
        public List<int> GetGridIndices(int standID)
        {
            List<int> result = new();
            RectangleF rect = mBoundingBoxByStandID[standID].Item1;
            GridWindowEnumerator<int> runner = new(this.Grid, rect);
            while (runner.MoveNext())
            {
                int cellStandID = runner.Current;
                if (cellStandID == standID)
                {
                    result.Add(runner.CurrentIndex);
                }
            }
            return result;
        }

        /// retrieve a list of saplings on a given stand polygon.
        //List<Tuple<ResourceUnitSpecies *, SaplingTreeOld *> > saplingTrees(int id)
        //{
        //    List<Tuple<ResourceUnitSpecies *, SaplingTreeOld *> > result;
        //    List<ResourceUnit> resource_units = resourceUnits(id);
        //    foreach(ResourceUnit *ru, resource_units) {
        //        foreach(ResourceUnitSpecies *rus, ru.ruSpecies()) {
        //            foreach(SaplingTreeOld &tree, rus.sapling().saplings()) {
        //                if (LIFgridValue( tree.coords() ) == id)
        //                    result.push_back( Tuple<ResourceUnitSpecies *, SaplingTreeOld *>(rus, &const_cast<SaplingTreeOld&>(tree)) );
        //            }
        //        }
        //    }
        //    qDebug() << "loaded" << result.count() << "sapling trees";
        //    return result;

        //}

        /// retrieve a list of all stands that are neighbors of the stand with ID "index".
        public List<int> GetNeighboringStandIDs(int standID)
        {
            if (mNeighborListByStandID.Count == 0)
            {
                this.BuildNeighborList(); // fill the list
            }
            return this.mNeighborListByStandID[standID];
        }

        /// scan the map and add neighborhood-relations to the mNeighborList
        /// the 4-neighborhood is used to identify neighbors.
        private void BuildNeighborList()
        {
            this.mNeighborListByStandID.Clear();

            GridWindowEnumerator<int> gridRuner = new(this.Grid, this.Grid.GetCellExtent()); // the full grid
            int[] neighbors4 = new int[4];
            while (gridRuner.MoveNext())
            {
                gridRuner.GetNeighbors4(neighbors4); // get the four-neighborhood (0-pointers possible)
                foreach (int neighborID in neighbors4)
                {
                    // TODO: why neighborID > 0?
                    if ((neighborID != 0) && (gridRuner.Current != neighborID))
                    {
                        // add both adjacencies
                        mNeighborListByStandID.AddToList(gridRuner.Current, neighborID);
                        mNeighborListByStandID.AddToList(neighborID, gridRuner.Current);
                    }
                }
            }
        }
    }
}
