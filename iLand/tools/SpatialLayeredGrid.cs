using iLand.core;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace iLand.tools
{
    internal class SpatialLayeredGrid
    {
        private List<string> mGridNames; ///< the list of grid names
        private List<Grid<float>> mGrids; ///< the grid

        public List<string> gridNames() { return mGridNames; }

        public SpatialLayeredGrid() 
        { 
            setup(); 
        }

        public double value(float x, float y, int index)
        {
            checkGrid(index); return mGrids[index].constValueAt(x, y);
        }

        public double value(PointF world_coord, int index)
        {
            checkGrid(index); 
            return mGrids[index].constValueAt(world_coord);
        }

        public double value(int ix, int iy, int index) 
        { 
            checkGrid(index); 
            return mGrids[index].constValueAtIndex(ix, iy); 
        }
        
        public double value(int grid_index, int index) 
        { 
            checkGrid(index); 
            return mGrids[index].constValueAtIndex(grid_index); 
        }
        
        public void range(out double rMin, out double rMax, int index)
        {
            rMin = 9999999999.0; 
            rMax = -99999999999.0;
            for (int i = 0; i < mGrids[index].count(); ++i)
            {
                rMin = Math.Min(rMin, value(i, index));
                rMax = Math.Max(rMax, value(i, index));
            }
        }

        ///< helper function that checks if grids are to be created
        private void checkGrid(int grid_index) 
        {
            if (mGrids[grid_index] != null)
            {
                this.createGrid(grid_index);
            }
        }

        public void setup()
        {
            addGrid("rumple", null);
        }

        public void createGrid(int grid_index)
        {
            // TODO: what should happen here?
        }

        public int addGrid(string name, Grid<float> grid)
        {
            mGridNames.Add(name);
            mGrids.Add(grid);
            return mGrids.Count;
        }
    }
}
