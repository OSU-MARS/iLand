using iLand.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace iLand.Test
{
    [TestClass]
    public class GridTest : LandTest
    {
        public TestContext? TestContext { get; set; }

        private static Grid<float> CreateAveragedGrid(int cellSize)
        {
            Grid<float> grid = new();
            grid.Setup(Constant.Grid.ResourceUnitSizeInM / cellSize, Constant.Grid.ResourceUnitSizeInM / cellSize, cellSize);
            for (int xIndex = 0; xIndex < grid.CellsX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.CellsY; ++yIndex)
                {
                    grid[xIndex, yIndex] += xIndex + yIndex; // include initialization to 0.0F in test coverage
                }
            }

            float cellArea = cellSize * cellSize;
            for (int xIndex = 0; xIndex < grid.CellsX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.CellsY; ++yIndex)
                {
                    grid[xIndex, yIndex] /= cellArea;
                }
            }
            return grid;
        }

        [TestMethod]
        public void AveragedGrid()
        {
            Grid<float> averaged = GridTest.CreateAveragedGrid(10);
            int count = 0;
            for (int index = 0; index < averaged.CellCount; ++index)
            {
                if (averaged[index] > 0.09)
                {
                    ++count;
                }
            }
            Assert.IsTrue(count == 55);
        }
    }
}
