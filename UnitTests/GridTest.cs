using iLand.core;
using iLand.tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace iLand.Test
{
    [TestClass]
    public class GridTest
    {
        public TestContext TestContext { get; set; }

        //private Grid<float> averaged(int factor, int offsetx = 0, int offsety = 0)
        //{
        //    Grid<float> target = new Grid<float>();
        //    target.setup(cellsize() * factor, sizeX() / factor, sizeY() / factor);
        //    int x, y;
        //    T sum = 0;
        //    target.initialize(sum);
        //    // sum over array of 2x2, 3x3, 4x4, ...
        //    for (x = offsetx; x < mSizeX; x++)
        //    {
        //        for (y = offsety; y < mSizeY; y++)
        //        {
        //            this[(x - offsetx) / factor, (y - offsety) / factor] += constValueAtIndex(x, y);
        //        }
        //    }
        //    // divide
        //    double fsquare = factor * factor;
        //    for (int xIndex = 0; xIndex < this.sizeX(); ++xIndex)
        //    {
        //        for (int yIndex = 0; yIndex < this.sizeY(); ++yIndex)
        //        {
        //            this[xIndex, yIndex] /= fsquare;
        //        }
        //    }
        //    return target;
        //}

        [TestMethod]
        public void test()
        {
            // Test-funktion: braucht 1/3 time von readGrid()
            using DebugTimer t = new DebugTimer("test");
            Grid<float> averaged = null; // TODO: this.averaged(10);
            int count = 0;
            for (float p = 0; p < averaged.count(); ++p)
            {
                if (p > 0.9)
                {
                    count++;
                }
            }
            this.TestContext.WriteLine(count + " LIF > 0.9 of " + averaged.count());
        }
    }
}
