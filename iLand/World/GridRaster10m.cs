using iLand.Extensions;
using iLand.Input;
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
    /// <remarks>
    /// TODO: replace this legacy glass with <see cref="Grid{T}"/>.
    /// The raster is currently loaded from an ESRI ASCII file and nearest-neighbor resampled to 10 m. The grid is clipped to the extent of 
    /// the world and Constant.NoDataInt32 is used for no_data_values.
    /// </remarks>
    public class GridRaster10m
    {
        // holds the extent of each rasterized polygon and a count of occupied pixels
        private readonly Dictionary<UInt32, (RectangleF BoundingBox, int OccupiedAreaInM2)> boundingBoxByRasterizedPolygonID;
        // holds a list of resource units + areas per rasterized polygon ID
        private readonly Dictionary<UInt32, List<(ResourceUnit ResourceUnit, float OccupiedAreaInRU)>> resourceUnitsByRasterizedPolygonID;

        public Grid<UInt32> Grid { get; private init; }

        public GridRaster10m()
        {
            this.boundingBoxByRasterizedPolygonID = [];
            this.resourceUnitsByRasterizedPolygonID = [];

            this.Grid = new();
        }

        public GridRaster10m(string filePath)
            : this()
        {
            this.LoadFromFile(filePath);
        }

        public void CreateIndex(Landscape landscape)
        {
            // reset spatial index
            this.boundingBoxByRasterizedPolygonID.Clear();
            this.resourceUnitsByRasterizedPolygonID.Clear();

            // build indices
            for (int gridIndex = 0; gridIndex < this.Grid.CellCount; ++gridIndex)
            {
                if (this.Grid[gridIndex] == Constant.NoDataUInt32)
                {
                    continue;
                }

                Point cellIndex = this.Grid.GetCellXYIndex(gridIndex);
                RectangleF cellExtent = this.Grid.GetCellProjectExtent(cellIndex);
                UInt32 rasterizedPolygonID = this.Grid[gridIndex];
                if (this.boundingBoxByRasterizedPolygonID.TryGetValue(rasterizedPolygonID, out (RectangleF BoundingBox, int OccupiedAreaInM2) data))
                {
                    data.BoundingBox = RectangleF.Union(data.BoundingBox, cellExtent);
                    data.OccupiedAreaInM2 += Constant.Grid.HeightCellAreaInM2;
                }
                else
                {
                    data = (cellExtent, Constant.Grid.HeightCellAreaInM2);
                    this.boundingBoxByRasterizedPolygonID.Add(rasterizedPolygonID, data);
                }
                
                ResourceUnit resourceUnit = landscape.GetResourceUnit(this.Grid.GetCellProjectCentroid(cellIndex));
                if (resourceUnit == null)
                {
                    continue;
                }
                // find all entries for the current grid id
                // TODO: why is lookup by grid index rather than stand ID?
                List<(ResourceUnit, float)> resourceUnitsInStand = this.resourceUnitsByRasterizedPolygonID[(UInt32)gridIndex];

                // look for the resource unit 'ru'
                bool ruFound = false;
                Debug.Assert(Constant.Grid.HeightCellsPerRUWidth * Constant.Grid.HeightCellsPerRUWidth == 100); // 100 height cells per RU -> 1% RU area per height cell
                Debug.Assert(this.Grid.CellSizeInM == Constant.Grid.HeightCellsPerRUWidth);
                for (int index = 0; index < resourceUnitsInStand.Count; ++index)
                {
                    (ResourceUnit ResourceUnit, float OccupiedAreaInRU) candidate = resourceUnitsInStand[index];
                    if (candidate.ResourceUnit == resourceUnit)
                    {
                        candidate.OccupiedAreaInRU += 0.01F; // 1 pixel = 1% of the area
                        ruFound = true;
                        break;
                    }
                }
                if (ruFound == false)
                {
                    this.resourceUnitsByRasterizedPolygonID.AddToList((UInt32)gridIndex, (resourceUnit, 0.01F)); // TODO: why add non-intersecting RUs with 0.01 instead of 0.0?
                }
            }
        }

        public int GetAreaInSquareMeters(UInt32 rasterizedPolygonID)
        { 
            return this.boundingBoxByRasterizedPolygonID[rasterizedPolygonID].OccupiedAreaInM2; // return the area (m²) covered by the polygon
        }

        public RectangleF GetBoundingBox(UInt32 rasterizedPolygonID)
        {
            return this.boundingBoxByRasterizedPolygonID[rasterizedPolygonID].BoundingBox;
        }

        /// return the grid value (stand ID, in typical use) at the coordinates *from* the LIF-Grid (i.e., 2m grid).
        public UInt32 GetPolygonIDFromLightGridIndex(Point lightGridXYIndex)
        {
            return this.Grid[lightGridXYIndex.X, lightGridXYIndex.Y, Constant.Grid.LightCellsPerHeightCellWidth];
        }

        /// returns a list with resource units and area factors per 'id'.
        /// the area is '1' if the resource unit is fully covered by the grid-value.
        public List<(ResourceUnit ResourceUnit, float OccupiedAreaInRU)> GetResourceUnitAreaFractions(UInt32 standID)
        {
            return this.resourceUnitsByRasterizedPolygonID[standID]; 
        }

        /// return a list of all living trees on the area denoted by 'id'
        public List<(TreeListSpatial, List<int>)> GetLivingTreesInStand(UInt32 standID)
        {
            List<(TreeListSpatial Trees, List<int> LiveTreeIndices)> livingTrees = [];
            IReadOnlyCollection<(ResourceUnit, float _)> resourceUnitsInStand = this.resourceUnitsByRasterizedPolygonID[standID];
            foreach ((ResourceUnit ResourceUnit, float _) unitInStand in resourceUnitsInStand)
            {
                SortedList<WorldFloraID, TreeListSpatial> treesBySpeciesID = unitInStand.ResourceUnit.Trees.TreesBySpeciesID;
                for (int speciesIndex = 0; speciesIndex < treesBySpeciesID.Count; ++speciesIndex)
                {
                    TreeListSpatial treesOfSpecies = treesBySpeciesID.Values[speciesIndex];
                    (TreeListSpatial Trees, List<int> LiveTreeIndices) livingTreesInStand = new(treesOfSpecies, []);
                    for (int treeIndex = 0; treeIndex < treesOfSpecies.Count; ++treeIndex)
                    {
                        if ((this.GetPolygonIDFromLightGridIndex(treesOfSpecies.LightCellIndexXY[treeIndex]) == standID) && (treesOfSpecies.IsDead(treeIndex) == false))
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
        public List<int> GetGridIndices(UInt32 rasterizedPolygonID)
        {
            RectangleF boundingBox = this.boundingBoxByRasterizedPolygonID[rasterizedPolygonID].BoundingBox;
            GridWindowEnumerator<UInt32> gridEnumerator10m = new(this.Grid, boundingBox);

            List<int> gridIndices = [];
            while (gridEnumerator10m.MoveNext())
            {
                UInt32 cellStandID = gridEnumerator10m.Current;
                if (cellStandID == rasterizedPolygonID)
                {
                    gridIndices.Add(gridEnumerator10m.CurrentIndex);
                }
            }
            return gridIndices;
        }

        /// returns true, if 'id' is a valid id in the grid, false otherwise.
        public bool IsIndexed(UInt32 rasterizedPolygonID)
        {
            return this.boundingBoxByRasterizedPolygonID.ContainsKey(rasterizedPolygonID);
        }

        public bool IsSetup()
        {
            return this.Grid.IsSetup();
        }

        // load ESRI ASCII raster
        public void LoadFromFile(string filePath)
        {
            EsriAsciiRasterReader raster = new(filePath);

            // clear any existing spatial indices
            this.boundingBoxByRasterizedPolygonID.Clear();
            this.resourceUnitsByRasterizedPolygonID.Clear();

            // create a grid with the same size as the height grid
            // (height-grid: 10m size, covering the full extent)
            this.Grid.Fill(0);
            this.Grid.Setup(raster.GetBoundingBox(), raster.CellSize);

            for (int gridIndex = 0; gridIndex < this.Grid.CellCount; ++gridIndex)
            {
                PointF cellCentroid = this.Grid.GetCellProjectCentroid(this.Grid.GetCellXYIndex(gridIndex));

                // TODO: relax assumption that raster's origin is (0, 0) in iLand's internal project coordinates
                float cellValue = raster.GetValue(cellCentroid);
                if (cellValue != raster.NoDataValue)
                {
                    this.Grid[gridIndex] = (UInt32)cellValue;
                }
                else
                {
                    this.Grid[gridIndex] = Constant.NoDataUInt32;
                }
            }
        }
    }
}
