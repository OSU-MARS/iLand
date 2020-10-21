using iLand.Simulation;
using iLand.Tools;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;

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
        // private static readonly MapGridRULock mapGridLock;

        private readonly Dictionary<int, MutableTuple<RectangleF, float>> mBoundingBoxByStandID; // holds the extent and area for each map-id
        private readonly Dictionary<int, List<MutableTuple<ResourceUnit, float>>> mResourceUnitsByStandID; // holds a list of resource units + areas per map-id
        private readonly Dictionary<int, List<int>> mNeighborListByStandID; // a list of neighboring polygons; for each ID all neighboring IDs are stored.

        public Grid<int> Grid { get; private set; }
        // file name of the grid
        public string Name { get; private set; }

        public double Area(int standID) { return this.IsValid(standID) ? mBoundingBoxByStandID[standID].Item2 : 0.0; } // return the area (m2) covered by the polygon
        public RectangleF BoundingBox(int id) { return this.IsValid(id) ? mBoundingBoxByStandID[id].Item1 : new RectangleF(); } // returns the bounding box of a polygon
        public bool IsValid() { return !this.Grid.IsEmpty(); }
        /// returns true, if 'id' is a valid id in the grid, false otherwise.
        public bool IsValid(int standID) { return mBoundingBoxByStandID.ContainsKey(standID); }

        //static MapGrid()
        //{
        //    mapGridLock = new MapGridRULock();
        //}

        public MapGrid()
        {
            this.Grid = new Grid<int>();
            this.mBoundingBoxByStandID = new Dictionary<int, MutableTuple<RectangleF, float>>();
            this.mResourceUnitsByStandID = new Dictionary<int, List<MutableTuple<ResourceUnit, float>>>();
            this.mNeighborListByStandID = new Dictionary<int, List<int>>();
        }

        public MapGrid(Model model, GisGrid sourceGrid)
        {
            LoadFromGrid(model, sourceGrid);
        }

        public MapGrid(Model model, string fileName, bool createIndex = true)
        {
            LoadFromFile(model, fileName, createIndex);
        }

        /// return true, if the point 'lif_grid_coords' (x/y integer key within the LIF-Grid)
        public bool HasValue(int id, Point lif_grid_coords)
        {
            return this.GetStandIDFromLightCoordinate(lif_grid_coords) == id;
        }

        /// return the stand-ID at the coordinates *from* the LIF-Grid (i.e., 2m grid).
        public int GetStandIDFromLightCoordinate(Point lif_grid_coords)
        {
            return Grid[lif_grid_coords.X / Constant.LightPerHeightSize, lif_grid_coords.Y / Constant.LightPerHeightSize];
        }

        // load from an already present GisGrid
        public bool LoadFromGrid(Model model, GisGrid source_grid, bool create_index = true)
        {
            if (model == null)
            {
                throw new NotSupportedException("GisGrid::create10mGrid: no valid model to retrieve height grid.");
            }

            Grid<HeightCell> h_grid = model.HeightGrid;
            if (h_grid == null || h_grid.IsEmpty())
            {
                throw new NotSupportedException("MapGrid.loadFromGrid(): no valid height grid to copy grid size.");
            }
            // create a grid with the same size as the height grid
            // (height-grid: 10m size, covering the full extent)
            Grid.Clear();
            Grid.Setup(h_grid.PhysicalExtent, h_grid.CellSize);

            RectangleF world = model.WorldExtentUnbuffered;
            for (int i = 0; i < Grid.Count; i++)
            {
                PointF p = Grid.GetCellCenterPoint(Grid.IndexOf(i));
                if (source_grid.GetValue(p) != source_grid.NoDataValue && world.Contains(p))
                {
                    Grid[i] = (int)source_grid.GetValue(p);
                }
                else
                {
                    Grid[i] = -1;
                }
            }

            // create spatial index
            mBoundingBoxByStandID.Clear();
            mResourceUnitsByStandID.Clear();

            if (create_index)
            {
                CreateIndex(model);
            }
            return true;
        }

        public void CreateEmptyGrid(Model model)
        {
            Grid<HeightCell> h_grid = model.HeightGrid;
            if (h_grid == null || h_grid.IsEmpty())
            {
                throw new NotSupportedException("GisGrid::createEmptyGrid: 10mGrid: no valid height grid to copy grid size.");
            }
            // create a grid with the same size as the height grid
            // (height-grid: 10m size, covering the full extent)
            Grid.Clear();
            Grid.Setup(h_grid.PhysicalExtent, h_grid.CellSize);

            for (int i = 0; i < Grid.Count; i++)
            {
                // PointF p = mGrid.cellCenterPoint(mGrid.indexOf(i)); // BUGBUG: why was this in C++ code?
                Grid[i] = 0;
            }

            // reset spatial index
            mBoundingBoxByStandID.Clear();
            mResourceUnitsByStandID.Clear();
        }

        public void CreateIndex(Model model)
        {
            // reset spatial index
            mBoundingBoxByStandID.Clear();
            mResourceUnitsByStandID.Clear();
            // create new
            for (int gridIndex = 0; gridIndex < this.Grid.Count; ++gridIndex)
            {
                if (this.Grid[gridIndex] == -1)
                {
                    continue;
                }
                MutableTuple<RectangleF, float> data = mBoundingBoxByStandID[gridIndex];
                data.Item1 = RectangleF.Union(data.Item1, this.Grid.GetCellRect(this.Grid.IndexOf(gridIndex)));
                data.Item2 += Constant.LightSize * Constant.LightPerHeightSize * Constant.LightSize * Constant.LightPerHeightSize; // 100m2

                ResourceUnit ru = model.GetResourceUnit(this.Grid.GetCellCenterPoint(this.Grid.IndexOf(gridIndex)));
                if (ru == null)
                {
                    continue;
                }
                // find all entries for the current grid id
                List<MutableTuple<ResourceUnit, float>> pos = mResourceUnitsByStandID[gridIndex].ToList();

                // look for the resource unit 'ru'
                bool ruFound = false;
                for (int index = 0; index < pos.Count; ++index)
                {
                    MutableTuple<ResourceUnit, float> candidate = pos[index];
                    if (candidate.Item1 == ru)
                    {
                        candidate.Item2 += 0.01F; // 1 pixel = 1% of the area
                        ruFound = true;
                        break;
                    }
                }
                if (ruFound == false)
                {
                    mResourceUnitsByStandID.AddToList(gridIndex, new MutableTuple<ResourceUnit, float>(ru, 0.01F));
                }
            }
        }

        // load ESRI style text file
        public bool LoadFromFile(Model model, string fileName, bool createIndex)
        {
            GisGrid gisGrid = new GisGrid();
            Name = "invalid";
            if (gisGrid.LoadFromFile(fileName))
            {
                Name = fileName;
                return LoadFromGrid(model, gisGrid, createIndex);
            }
            return false;
        }

        /// returns a list with resource units and area factors per 'id'.
        /// the area is '1' if the resource unit is fully covered by the grid-value.
        public IReadOnlyCollection<MutableTuple<ResourceUnit, float>> ResourceUnitAreas(int standID)
        {
            return mResourceUnitsByStandID[standID]; 
        }

        /// returns the list of resource units with at least one pixel within the area designated by 'id'
        public List<ResourceUnit> GetResourceUnitsInStand(int standID)
        {
            IReadOnlyCollection<MutableTuple<ResourceUnit, float>> list = mResourceUnitsByStandID[standID];
            List<ResourceUnit> resourceUnits = new List<ResourceUnit>(list.Count);
            foreach (MutableTuple<ResourceUnit, float> ru in list)
            {
                resourceUnits.Add(ru.Item1);
            }
            return resourceUnits;
        }

        /// return a list of all living trees on the area denoted by 'id'
        public List<MutableTuple<Trees, List<int>>> GetLivingTreesInStand(int standID)
        {
            List<MutableTuple<Trees, List<int>>> livingTrees = new List<MutableTuple<Trees, List<int>>>();
            List<ResourceUnit> resourceUnitsInStand = this.GetResourceUnitsInStand(standID);
            foreach (ResourceUnit ru in resourceUnitsInStand)
            {
                foreach (Trees trees in ru.TreesBySpeciesID.Values)
                {
                    MutableTuple<Trees, List<int>> livingTreesInStand = new MutableTuple<Trees, List<int>>()
                    {
                        Item1 = trees,
                        Item2 = new List<int>()
                    };
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

        public int LoadTrees(Model model, int id, List<MutableTuple<Tree.Trees, double>> rList, string filter, int n_estimate)
        {
            rList.Clear();
            if (n_estimate > 0)
            {
                rList.Capacity = n_estimate;
            }
            Expression expression = null;
            TreeWrapper treeWrapper = new TreeWrapper();
            if (String.IsNullOrEmpty(filter) == false)
            {
                expression = new Expression(filter, treeWrapper);
                expression.EnableIncrementalSum();
            }
            // lock the resource units: removed again, WR20140821
            // mapGridLock.lock(id, resource_units);

            List<ResourceUnit> resourceUnitsInStand = GetResourceUnitsInStand(id);
            foreach (ResourceUnit ru in resourceUnitsInStand)
            {
                foreach (Trees trees in ru.TreesBySpeciesID.Values)
                {
                    for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                    {
                        if ((this.GetStandIDFromLightCoordinate(trees.LightCellPosition[treeIndex]) == id) && (trees.IsDead(treeIndex) == false))
                        {
                            treeWrapper.Trees = trees;
                            if (expression != null)
                            {
                                double value = expression.Evaluate(model, treeWrapper);
                                // keep if expression returns true (1)
                                bool loadTree = value == 1.0;
                                // if value is >0 (i.e. not "false"), then draw a random number
                                if ((loadTree == false) && (value > 0.0))
                                {
                                    loadTree = model.RandomGenerator.Random() < value;
                                }
                                if (loadTree == false)
                                {
                                    continue;
                                }
                            }
                            rList.Add(new MutableTuple<Tree.Trees, double>(trees, 0.0));
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
            List<int> result = new List<int>();
            RectangleF rect = mBoundingBoxByStandID[standID].Item1;
            GridRunner<int> runner = new GridRunner<int>(Grid, rect);
            for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
            {
                int cellStandID = runner.Current;
                if (cellStandID == standID)
                {
                    result.Add(cellStandID - Grid[0]);
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
        public List<int> GetNeighborsOf(int index)
        {
            if (mNeighborListByStandID.Count == 0)
            {
                this.BuildNeighborList(); // fill the list
            }
            return mNeighborListByStandID[index];
        }

        /// scan the map and add neighborhood-relations to the mNeighborList
        /// the 4-neighborhood is used to identify neighbors.
        private void BuildNeighborList()
        {
            mNeighborListByStandID.Clear();

            GridRunner<int> gridRuner = new GridRunner<int>(this.Grid, this.Grid.CellExtent()); // the full grid
            int[] neighbors4 = new int[4];
            for (gridRuner.MoveNext(); gridRuner.IsValid(); gridRuner.MoveNext())
            {
                gridRuner.Neighbors4(neighbors4); // get the four-neighborhood (0-pointers possible)
                foreach (int neighborID in neighbors4)
                {
                    // TODO: neighborID > 0?
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
