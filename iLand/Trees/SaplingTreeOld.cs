using iLand.Tools;
using iLand.World;
using System;
using System.Drawing;

namespace iLand.Trees
{
    // TODO: consolidate into SaplingTree
    internal class SaplingTreeOld
    {
        public class AgeStressYears
        {
            public UInt16 Age { get; set; }  // number of consectuive years the sapling suffers from dire conditions
            public UInt16 StressYears { get; set; } // (upper 16bits) + age of sapling (lower 16 bits)
        }

        public AgeStressYears Age { get; private set; }
        public float Height { get; set; } // height of the sapling in meter
        public int LightPixel { get; set; } // pointer to the lifpixel the sapling lives on, set to 0 if sapling died/removed

        public SaplingTreeOld()
        {
            Age = new AgeStressYears()
            {
                Age = 0,
                StressYears = 0
            };
            LightPixel = -1;
            Height = 0.05F;
        }

        public Point Coordinate(Grid<float> lightGrid)
        {
            return lightGrid.IndexOf(LightPixel);
        }

        public bool IsValid()
        {
            return LightPixel != -1;
        }
    }
}
