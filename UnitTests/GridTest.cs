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
            Grid<float> grid = new Grid<float>();
            grid.Setup(Constant.RUSize / cellSize, Constant.RUSize / cellSize, cellSize);
            for (int xIndex = 0; xIndex < grid.SizeX; xIndex++)
            {
                for (int yIndex = 0; yIndex < grid.SizeY; yIndex++)
                {
                    grid[xIndex, yIndex] += xIndex + yIndex; // include initialization to 0.0F in test coverage
                }
            }

            float cellArea = cellSize * cellSize;
            for (int xIndex = 0; xIndex < grid.SizeX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < grid.SizeY; ++yIndex)
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
            for (int index = 0; index < averaged.Count; ++index)
            {
                if (averaged[index] > 0.09)
                {
                    count++;
                }
            }
            Assert.IsTrue(count == 55);
        }
    }
}
