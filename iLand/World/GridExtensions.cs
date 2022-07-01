using System;
using System.Drawing;

namespace iLand.World
{
    internal static class GridExtensions
    {
        /** retrieve from the index from an element reversely from a pointer to that element.
            The internal memory layout is (for dimx=6, dimy=3):
            0  1  2  3  4  5
            6  7  8  9  10 11
            12 13 14 15 16 17
            Note: north and south are reversed, thus the item with index 0 is located in the south-western edge of the grid! */
        public static Point CellIndexOf<T>(this Grid<T> grid, T element) where T : class?
        {
            //    Point result(-1,-1);
            if (element != null)
            {
                for (int idx = 0; idx < grid.CellCount; ++idx)
                {
                    if (grid[idx] == element)
                    {
                        return grid.GetCellXYIndex(idx);
                    }
                }
            }
            return new Point(-1, -1);
        }

        public static void Limit(this Grid<int> grid, int min_value, int max_value)
        {
            for (int xIndex = 0; xIndex < grid.SizeX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.SizeY; ++yIndex)
                {
                    int value = grid[xIndex, yIndex];
                    if (value > max_value)
                    {
                        grid[xIndex, yIndex] = max_value;
                    }
                    else if (value < min_value)
                    {
                        grid[xIndex, yIndex] = min_value;
                    }
                }
            }
        }

        public static float Max(this Grid<float> grid)
        {
            float maxv = float.MinValue;
            for (int xIndex = 0; xIndex < grid.SizeX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.SizeY; ++yIndex)
                {
                    maxv = Math.Max(maxv, grid[xIndex, yIndex]);
                }
            }
            return maxv;
        }

        public static void Multiply(this Grid<float> grid, float factor)
        {
            for (int xIndex = 0; xIndex < grid.SizeX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.SizeY; ++yIndex)
                {
                    grid[xIndex, yIndex] *= factor;
                }
            }
        }

        public static float Sum(this Grid<float> grid)
        {
            float total = 0;
            for (int xIndex = 0; xIndex < grid.SizeX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.SizeY; ++yIndex)
                {
                    total += grid[xIndex, yIndex];
                }
            }
            return total;
        }

        //public static Grid<double> ToDouble(this Grid<int> grid)
        //{
        //    Grid<double> doubleGrid = new Grid<double>();
        //    doubleGrid.Setup(grid.PhysicalExtent, grid.CellSize);
        //    for (int xIndex = 0; xIndex < grid.CellsX; ++xIndex)
        //    {
        //        for (int yIndex = 0; yIndex < grid.CellsY; ++yIndex)
        //        {
        //            doubleGrid[xIndex, yIndex] = grid[xIndex, yIndex];
        //        }
        //    }
        //    return doubleGrid;
        //}
    }
}
