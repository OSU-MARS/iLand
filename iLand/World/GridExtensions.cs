using System;
using System.Drawing;

namespace iLand.World
{
    internal static class GridExtensions
    {
        public static void Limit(this Grid<int> grid, int min_value, int max_value)
        {
            for (int xIndex = 0; xIndex < grid.CellsX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.CellsY; ++yIndex)
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
            float maxValue = Single.MinValue;
            for (int xIndex = 0; xIndex < grid.CellsX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.CellsY; ++yIndex)
                {
                    maxValue = MathF.Max(maxValue, grid[xIndex, yIndex]);
                }
            }
            return maxValue;
        }

        public static void Multiply(this Grid<float> grid, float factor)
        {
            for (int xIndex = 0; xIndex < grid.CellsX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.CellsY; ++yIndex)
                {
                    grid[xIndex, yIndex] *= factor;
                }
            }
        }

        public static float Sum(this Grid<float> grid)
        {
            float total = 0;
            for (int xIndex = 0; xIndex < grid.CellsX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.CellsY; ++yIndex)
                {
                    total += grid[xIndex, yIndex];
                }
            }
            return total;
        }
    }
}
