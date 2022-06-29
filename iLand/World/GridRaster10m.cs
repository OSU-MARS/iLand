using iLand.Input;
using iLand.Tool;
using iLand.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;

namespace iLand.World
{
    /// <summary>
    /// A grid with canopy height, stand classification, or other data at 10 m resolution.
    /// </summary>
    // The raster is currently loaded from an ESRI ASCII file and nearest-neighbor resampled to 10 m. The grid is clipped to the extent of the
    // world and Constant.NoDataInt32 is used for no_data_values.
    public class GridRaster10m
    {
        // holds the extent of each rasterized polygon and a count of occupied pixels
        private readonly Dictionary<int, (RectangleF BoundingBox, float OccupiedAreaInM2)> boundingBoxByRasterizedPolygonID;
        // holds a list of resource units + areas per rasterized polygon ID
        private readonly Dictionary<int, List<(ResourceUnit RU, float OccupiedAreaInRU)>> resourceUnitsByRasterizedPolygonID;

        public Grid<int> Grid { get; private init; }

        public GridRaster10m()
        {
            this.boundingBoxByRasterizedPolygonID = new Dictionary<int, (RectangleF, float)>();
            this.resourceUnitsByRasterizedPolygonID = new Dictionary<int, List<(ResourceUnit, float)>>();

            this.Grid = new Grid<int>();
        }

        public GridRaster10m(Landscape landscape, string filePath)
            : this()
        {
            this.LoadFromFile(landscape, filePath);
        }

        public void CreateIndex(Landscape landscape)
        {
            // reset spatial index
            this.boundingBoxByRasterizedPolygonID.Clear();
            this.resourceUnitsByRasterizedPolygonID.Clear();

            // build indices
            for (int gridIndex = 0; gridIndex < this.Grid.Count; ++gridIndex)
            {
                if (this.Grid[gridIndex] == Constant.NoDataInt32)
                {
                    continue;
                }

                Point cellIndex = this.Grid.GetCellXYIndex(gridIndex);
                RectangleF cellExtent = this.Grid.GetCellExtent(cellIndex);
                int rasterizedPolygonID = this.Grid[gridIndex];
                if (this.boundingBoxByRasterizedPolygonID.TryGetValue(rasterizedPolygonID, out (RectangleF BoundingBox, float OccupiedAreaInM2) data))
                {
                    data.BoundingBox = RectangleF.Union(data.BoundingBox, cellExtent);
                    data.OccupiedAreaInM2 += Constant.HeightCellAreaInM2;
                }
                else
                {
                    data = (cellExtent, Constant.HeightCellAreaInM2);
                    this.boundingBoxByRasterizedPolygonID.Add(rasterizedPolygonID, data);
                }
                
                ResourceUnit ru = landscape.GetResourceUnit(this.Grid.GetCellCentroid(cellIndex));
                if (ru == null)
                {
                    continue;
                }
                // find all entries for the current grid id
                // TODO: why is lookup by grid index rather than stand ID?
                List<(ResourceUnit, float)> resourceUnitsInStand = this.resourceUnitsByRasterizedPolygonID[gridIndex];

                // look for the resource unit 'ru'
                bool ruFound = false;
                Debug.Assert(Constant.HeightSizePerRU * Constant.HeightSizePerRU == 100); // 100 height cells per RU -> 1% RU area per height cell
                Debug.Assert(this.Grid.CellSize == Constant.HeightSizePerRU);
                for (int index = 0; index < resourceUnitsInStand.Count; ++index)
                {
                    (ResourceUnit RU, float OccupiedAreaInRU) candidate = resourceUnitsInStand[index];
                    if (candidate.RU == ru)
                    {
                        candidate.OccupiedAreaInRU += 0.01F; // 1 pixel = 1% of the area
                        ruFound = true;
                        break;
                    }
                }
                if (ruFound == false)
                {
                    this.resourceUnitsByRasterizedPolygonID.AddToList(gridIndex, (ru, 0.01F)); // TODO: why add non-intersecting RUs with 0.01 instead of 0.0?
                }
            }
        }

        public float GetAreaInSquareMeters(int rasterizedPolygonID)
        { 
            return this.boundingBoxByRasterizedPolygonID[rasterizedPolygonID].OccupiedAreaInM2; // return the area (m²) covered by the polygon
        }

        public RectangleF GetBoundingBox(int rasterizedPolygonID)
        {
            return this.boundingBoxByRasterizedPolygonID[rasterizedPolygonID].BoundingBox;
        }

        /// return the stand-ID at the coordinates *from* the LIF-Grid (i.e., 2m grid).
        public int GetStandIDFromLightCoordinate(Point lightCoordinate)
        {
            return this.Grid[lightCoordinate.X, lightCoordinate.Y, Constant.LightCellsPerHeightSize];
        }

        /// returns a list with resource units and area factors per 'id'.
        /// the area is '1' if the resource unit is fully covered by the grid-value.
        public IList<(ResourceUnit, float)> GetResourceUnitAreaFractions(int standID)
        {
            return this.resourceUnitsByRasterizedPolygonID[standID]; 
        }

        /// return a list of all living trees on the area denoted by 'id'
        public List<(Trees, List<int>)> GetLivingTreesInStand(int standID)
        {
            List<(Trees Trees, List<int> LiveTreeIndices)> livingTrees = new();
            IReadOnlyCollection<(ResourceUnit, float)> resourceUnitsInStand = this.resourceUnitsByRasterizedPolygonID[standID];
            foreach ((ResourceUnit RU, float) ru in resourceUnitsInStand)
            {
                foreach (Trees trees in ru.RU.Trees.TreesBySpeciesID.Values)
                {
                    (Trees Trees, List<int> LiveTreeIndices) livingTreesInStand = new(trees, new List<int>());
                    for (int treeIndex = 0; treeIndex < trees.Count; ++treeIndex)
                    {
                        if ((this.GetStandIDFromLightCoordinate(trees.LightCellPosition[treeIndex]) == standID) && (trees.IsDead(treeIndex) == false))
                        {
                            livingTreesInStand.LiveTreeIndices.Add(treeIndex);
                        }
                    }

                    livingTrees.Add(livingTreesInStand);
                }
            }
            return livingTrees;
        }

        /// return a list of grid-indices of a given stand-id (a grid-index
        /// is the index of 10m x 10m pixels within the internal storage)
        /// The selection is limited to pixels within the world's extent
        public List<int> GetGridIndices(int rasterizedPolygonID)
        {
            RectangleF boundingBox = boundingBoxByRasterizedPolygonID[rasterizedPolygonID].BoundingBox;
            GridWindowEnumerator<int> gridRunner = new(this.Grid, boundingBox);

            List<int> gridIndices = new();
            while (gridRunner.MoveNext())
            {
                int cellStandID = gridRunner.Current;
                if (cellStandID == rasterizedPolygonID)
                {
                    gridIndices.Add(gridRunner.CurrentIndex);
                }
            }
            return gridIndices;
        }

        /// returns true, if 'id' is a valid id in the grid, false otherwise.
        public bool IsIndexed(int rasterizedPolygonID)
        {
            return this.boundingBoxByRasterizedPolygonID.ContainsKey(rasterizedPolygonID);
        }

        public bool IsSetup()
        {
            return this.Grid.IsSetup();
        }

        // load ESRI ASCII raster
        public void LoadFromFile(Landscape landscape, string filePath)
        {
            if ((landscape.HeightGrid == null) || (landscape.HeightGrid.IsSetup() == false))
            {
                throw new ArgumentOutOfRangeException(nameof(landscape), "No height grid available for initializing grid size.");
            }

            EsriAsciiRasterReader raster = new(filePath);

            // clear any existing spatial indices
            this.boundingBoxByRasterizedPolygonID.Clear();
            this.resourceUnitsByRasterizedPolygonID.Clear();

            Grid<HeightCell> heightGrid = landscape.HeightGrid;
            // create a grid with the same size as the height grid
            // (height-grid: 10m size, covering the full extent)
            this.Grid.Clear();
            this.Grid.Setup(heightGrid.PhysicalExtent, heightGrid.CellSize);

            for (int gridIndex = 0; gridIndex < this.Grid.Count; ++gridIndex)
            {
                PointF cellCentroid = this.Grid.GetCellCentroid(this.Grid.GetCellXYIndex(gridIndex));
                Debug.Assert(landscape.Extent.Contains(cellCentroid));

                // TODO: relax assumption that raster's origin is (0, 0) in iLand's internal project coordinates
                float cellValue = raster.GetValue(cellCentroid);
                if (cellValue != raster.NoDataValue)
                {
                    this.Grid[gridIndex] = (int)cellValue;
                }
                else
                {
                    this.Grid[gridIndex] = Constant.NoDataInt32;
                }
            }
        }
    }
}
