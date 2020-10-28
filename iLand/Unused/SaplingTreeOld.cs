using iLand.Tools;
using iLand.World;
using System;
using System.Drawing;

namespace iLand.Tree
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
        public int LightPixelIndex { get; set; } // pointer to the lifpixel the sapling lives on, set to 0 if sapling died/removed

        public SaplingTreeOld()
        {
            Age = new AgeStressYears()
            {
                Age = 0,
                StressYears = 0
            };
            LightPixelIndex = -1;
            Height = Constant.Sapling.MinimumHeight;
        }

        public Point Coordinate(Grid<float> lightGrid)
        {
            return lightGrid.GetCellPosition(LightPixelIndex);
        }

        public bool IsValid()
        {
            return LightPixelIndex != -1;
        }
    }
}
