namespace iLand.Core
{
    public class HeightGridValue
    {
        // TODO: split into two variables
        private int mCount; // the lower 16 bits are to count, the higher for flags. bit 16: valid (0=valid, 1=outside of project area)

        ///< dominant tree height (m)
        public float Height { get; set; }

        public void Init(float height, int count) 
        {
            this.mCount = count;
            this.Height = height; 
        }

        ///< get count of trees on pixel
        public int Count() { return mCount & 0x0000ffff; }

        public void IncreaseCount() { mCount++; } ///< increase the number of trees on pixel
        public bool IsInWorld() { return Global.IsBitSet(mCount, 16) == false; } ///< a value of 1: not valid (returns false)
        public bool IsOutsideWorld() { return Global.IsBitSet(mCount, 17); }
        public bool IsRadiating() { return Global.IsBitSet(mCount, 18); }

        ///< set the count to 0
        public void ResetCount() 
        { 
            mCount &= unchecked((int)0xffff0000); 
        }

        public void SetIsOutsideWorld(bool isOutside) 
        { 
            Global.SetBit(ref mCount, 17, isOutside); 
        }

        ///< bit 18: if set, the pixel is actively radiating influence on the LIF (such pixels are on the edge of "forestOutside")
        public void SetIsRadiating() 
        { 
            Global.SetBit(ref mCount, 18, true); 
        }

        ///< set bit to 1: pixel is not valid
        public void SetInWorld(bool valid) 
        { 
            Global.SetBit(ref mCount, 16, !valid); 
        }
    }
}
