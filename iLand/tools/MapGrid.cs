using iLand.Core;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace iLand.Tools
{
    /** MapGrid encapsulates maps that classify the area in 10m resolution (e.g. for stand-types, management-plans, ...)
      @ingroup tools
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

        private readonly Dictionary<int, MutableTuple<RectangleF, double>> mRectIndex; ///< holds the extent and area for each map-id
        private readonly MultiValueDictionary<int, MutableTuple<ResourceUnit, double>> mRUIndex; ///< holds a list of resource units + areas per map-id
        private readonly MultiValueDictionary<int, int> mNeighborList; ///< a list of neighboring polygons; for each ID all neighboring IDs are stored.

        public Grid<int> Grid { get; private set; }
        ///< file name of the grid
        public string Name { get; private set; }

        public double Area(int id) { return IsValid(id) ? mRectIndex[id].Item2 : 0.0; } ///< return the area (m2) covered by the polygon
        public RectangleF BoundingBox(int id) { return IsValid(id) ? mRectIndex[id].Item1 : new RectangleF(); } ///< returns the bounding box of a polygon
        public bool IsValid() { return !Grid.IsEmpty(); }
        /// returns true, if 'id' is a valid id in the grid, false otherwise.
        public bool IsValid(int id) { return mRectIndex.ContainsKey(id); }

        //static MapGrid()
        //{
        //    mapGridLock = new MapGridRULock();
        //}

        public MapGrid()
        {
            this.Grid = new Grid<int>();
            this.mRectIndex = new Dictionary<int, MutableTuple<RectangleF, double>>();
            this.mRUIndex = new MultiValueDictionary<int, MutableTuple<ResourceUnit, double>>();
            this.mNeighborList = new MultiValueDictionary<int, int>();
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
            return this.StandIDFromLifCoord(lif_grid_coords) == id;
        }

        /// return the stand-ID at the coordinates *from* the LIF-Grid (i.e., 2m grid).
        public int StandIDFromLifCoord(Point lif_grid_coords)
        {
            return Grid[lif_grid_coords.X / Constant.LightPerHeightSize, lif_grid_coords.Y / Constant.LightPerHeightSize];
        }

        ///< load from an already present GisGrid
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
            mRectIndex.Clear();
            mRUIndex.Clear();

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
            mRectIndex.Clear();
            mRUIndex.Clear();
        }

        public void CreateIndex(Model model)
        {
            // reset spatial index
            mRectIndex.Clear();
            mRUIndex.Clear();
            // create new
            for (int p = 0; p < Grid.Count; ++p)
            {
                if (Grid[p] == -1)
                {
                    continue;
                }
                MutableTuple<RectangleF, double> data = mRectIndex[p];
                data.Item1 = RectangleF.Union(data.Item1, Grid.GetCellRect(Grid.IndexOf(p)));
                data.Item2 += Constant.LightSize * Constant.LightPerHeightSize * Constant.LightSize * Constant.LightPerHeightSize; // 100m2

                ResourceUnit ru = model.GetResourceUnit(Grid.GetCellCenterPoint(Grid.IndexOf(p)));
                if (ru == null)
                {
                    continue;
                }
                // find all entries for the current grid id
                List<MutableTuple<ResourceUnit, double>> pos = mRUIndex[p].ToList();

                // look for the resource unit 'ru'
                bool found = false;
                for (int index = 0; index < pos.Count; ++index)
                {
                    MutableTuple<ResourceUnit, double> candidate = pos[index];
                    if (candidate.Item1 == ru)
                    {
                        candidate.Item2 += 0.01; // 1 pixel = 1% of the area
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    mRUIndex.Add(p, new MutableTuple<ResourceUnit, double>(ru, 0.01));
                }
            }
        }

        ///< load ESRI style text file
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
        public IReadOnlyCollection<MutableTuple<ResourceUnit, double>> ResourceUnitAreas(int id)
        {
            return mRUIndex[id]; 
        }

        /// returns the list of resource units with at least one pixel within the area designated by 'id'
        public List<ResourceUnit> ResourceUnits(int id)
        {
            List<ResourceUnit> result = new List<ResourceUnit>();
            IReadOnlyCollection<MutableTuple<ResourceUnit, double>> list = mRUIndex[id];
            foreach (MutableTuple<ResourceUnit, double> ru in list)
            {
                result.Add(ru.Item1);
            }
            return result;
        }

        /// return a list of all living trees on the area denoted by 'id'
        public List<Tree> Trees(int id)
        {
            List<Tree> tree_list = new List<Tree>();
            List<ResourceUnit> resource_units = ResourceUnits(id);
            foreach (ResourceUnit ru in resource_units)
            {
                foreach (Tree tree in ru.Trees)
                {
                    if (StandIDFromLifCoord(tree.LightCellPosition) == id && !tree.IsDead())
                    {
                        tree_list.Add(tree);
                    }
                }
            }
            //    qDebug() << "trees: found" << c << "/" << tree_list.size();
            return tree_list;
        }

        public int LoadTrees(GlobalSettings globalSettings, int id, List<MutableTuple<Tree, double>> rList, string filter, int n_estimate)
        {
            rList.Clear();
            if (n_estimate > 0)
            {
                rList.Capacity = n_estimate;
            }
            Expression expression = null;
            TreeWrapper tw = new TreeWrapper();
            if (String.IsNullOrEmpty(filter) == false)
            {
                expression = new Expression(filter, tw);
                expression.EnableIncrementalSum();
            }
            List<ResourceUnit> resource_units = ResourceUnits(id);
            // lock the resource units: removed again, WR20140821
            // mapGridLock.lock(id, resource_units);

            foreach (ResourceUnit ru in resource_units)
            {
                foreach (Tree tree in ru.Trees)
                {
                    if (StandIDFromLifCoord(tree.LightCellPosition) == id && !tree.IsDead())
                    {
                        Tree t = tree;
                        tw.Tree = t;
                        if (expression != null)
                        {
                            double value = expression.Calculate(tw, globalSettings);
                            // keep if expression returns true (1)
                            bool keep = value == 1.0;
                            // if value is >0 (i.e. not "false"), then draw a random number
                            if (!keep && value > 0.0)
                            {
                                keep = RandomGenerator.Random() < value;
                            }
                            if (!keep)
                            {
                                continue;
                            }
                        }
                        rList.Add(new MutableTuple<Tree, double>(t, 0.0));
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
        public List<int> GridIndices(int id)
        {
            List<int> result = new List<int>();
            RectangleF rect = mRectIndex[id].Item1;
            GridRunner<int> runner = new GridRunner<int>(Grid, rect);
            for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
            {
                int cell = runner.Current;
                if (cell == id)
                {
                    result.Add(cell - Grid[0]);
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
        public List<int> NeighborsOf(int index)
        {
            if (mNeighborList.Count == 0)
            {
                this.UpdateNeighborList(); // fill the list
            }
            return mNeighborList[index].ToList();
        }

        /// scan the map and add neighborhood-relations to the mNeighborList
        /// the 4-neighborhood is used to identify neighbors.
        public void UpdateNeighborList()
        {
            mNeighborList.Clear();
            GridRunner<int> gr = new GridRunner<int>(Grid, Grid.CellExtent()); // the full grid
            int[] n4 = new int[4];
            for (gr.MoveNext(); gr.IsValid(); gr.MoveNext())
            {
                gr.Neighbors4(n4); // get the four-neighborhood (0-pointers possible)
                for (int i = 0; i < 4; ++i)
                {
                    if (n4[i] != 0 && gr.Current != n4[i])
                    {
                        // look if we already have the pair
                        if (mNeighborList.ContainsKey(gr.Current) == false)
                        {
                            // add the "edge" two times in the hash
                            mNeighborList.Add(gr.Current, n4[i]);
                        }
                        if (mNeighborList.ContainsKey(n4[i]) == false)
                        {
                            mNeighborList.Add(n4[i], gr.Current);
                        }
                    }
                }
            }
        }
    }
}
