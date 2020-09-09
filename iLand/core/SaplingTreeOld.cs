using iLand.tools;
using System;
using System.Drawing;

namespace iLand.core
{
    internal class SaplingTreeOld
    {
        public struct AgeStressYears
        {
            public UInt16 age;  // number of consectuive years the sapling suffers from dire conditions
            public UInt16 stress_years; // (upper 16bits) + age of sapling (lower 16 bits)
        }

        public AgeStressYears age;
        public float height; // height of the sapling in meter
        public int pixel; // pointer to the lifpixel the sapling lives on, set to 0 if sapling died/removed

        public SaplingTreeOld()
        {
            age = new AgeStressYears()
            {
                age = 0,
                stress_years = 0
            };
            pixel = -1;
            height = 0.05F;
        }

        public Point coords()
        {
            return GlobalSettings.instance().model().grid().indexOf(pixel);
        }

        public bool isValid()
        {
            return pixel != -1;
        }
    }
}
