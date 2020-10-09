using iLand.Core;
using iLand.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace iLand.Test
{
    [TestClass]
    public class GridTest : LandTest
    {
        public TestContext TestContext { get; set; }

        private Grid<float> CreateAveragedGrid(int cellSize)
        {
            Grid<float> grid = new Grid<float>();
            grid.Setup(cellSize, Constant.RUSize / cellSize, Constant.RUSize / cellSize);
            for (int xIndex = 0; xIndex < grid.CellsX; xIndex++)
            {
                for (int yIndex = 0; yIndex < grid.CellsY; yIndex++)
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
            using DebugTimer t = new DebugTimer("GridTest.AveragedGrid()");
            Grid<float> averaged = this.CreateAveragedGrid(10);
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

        [TestMethod]
        public void RumpleIndex()
        {
            Model model = this.LoadProject(this.GetDefaultProjectPath(this.TestContext));
            RumpleIndex rumpleIndex = new RumpleIndex();
            rumpleIndex.Calculate(model);
            double index = rumpleIndex.Value(model);
            Assert.IsTrue(Math.Abs(index - 0.0) < 0.001);

            // check calculation: numbers for Jenness paper
            //float[] hs = new float[] { 165, 170, 145, 160, 183, 155, 122, 175, 190 };
            //double area = rumpleIndex.CalculateSurfaceArea(hs, 100);
        }
    }
}
