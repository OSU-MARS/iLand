using System;
using System.Drawing;

namespace iLand.World
{
    internal static class GridExtensions
    {
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
