using iLand.core;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace iLand.tools
{
    internal class SpatialLayeredGrid
    {
        private readonly List<Grid<float>> mGrids; ///< the grid

        public List<string> GridNames { get; private set; }

        public SpatialLayeredGrid() 
        {
            this.GridNames = new List<string>();
            this.mGrids = new List<Grid<float>>();

            Setup(); 
        }

        public double Value(float x, float y, int index)
        {
            CheckGrid(index); return mGrids[index][x, y];
        }

        public double Value(PointF world_coord, int index)
        {
            CheckGrid(index); 
            return mGrids[index][world_coord];
        }

        public double Value(int ix, int iy, int index) 
        { 
            CheckGrid(index); 
            return mGrids[index][ix, iy];
        }
        
        public double Value(int grid_index, int index) 
        { 
            CheckGrid(index); 
            return mGrids[index][grid_index];
        }
        
        public void Range(out double rMin, out double rMax, int index)
        {
            rMin = 9999999999.0; 
            rMax = -99999999999.0;
            for (int i = 0; i < mGrids[index].Count; ++i)
            {
                rMin = Math.Min(rMin, Value(i, index));
                rMax = Math.Max(rMax, Value(i, index));
            }
        }

        ///< helper function that checks if grids are to be created
        private void CheckGrid(int grid_index) 
        {
            if (mGrids[grid_index] != null)
            {
                this.CreateGrid(grid_index);
            }
        }

        public void Setup()
        {
            AddGrid("rumple", null);
        }

        public void CreateGrid(int grid_index)
        {
            // TODO: what should happen here?
        }

        public int AddGrid(string name, Grid<float> grid)
        {
            GridNames.Add(name);
            mGrids.Add(grid);
            return mGrids.Count;
        }
    }
}
