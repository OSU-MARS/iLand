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

            
            Assert.IsTrue((averaged.CellCount == 100) && (averaged.CellSizeInM == 10.0F) && (averaged.CellsX == 10) && (averaged.CellsY == 10) && (averaged.Data.Length == 100));
            Assert.IsTrue((averaged.ProjectExtent.X == 0.0F) && (averaged.ProjectExtent.Y == 0.0F) && (averaged.ProjectExtent.Height == 100.0F) && (averaged.ProjectExtent.Width == 100.0F) && 
                          (averaged.ProjectExtent.Top == 0.0F) && (averaged.ProjectExtent.Left == 0.0F) && (averaged.ProjectExtent.Right == 100.0F) && (averaged.ProjectExtent.Bottom == 100.0F) &&
                          (averaged.ProjectExtent.IsEmpty == false));
            Assert.IsTrue((averaged[0, 0] == 0.0F) && (averaged[0, 9] == 0.09F) && (averaged[9, 0] == 0.09F) && (averaged[9, 9] == 0.18F));
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
