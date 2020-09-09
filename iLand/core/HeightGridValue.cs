namespace iLand.core
{
    internal class HeightGridValue
    {
        private int mCount; // the lower 16 bits are to count, the higher for flags. bit 16: valid (0=valid, 1=outside of project area)

        public float height; ///< dominant tree height (m)

        public void init(float aheight, int acount) { height = aheight; mCount = acount; }

        public int count() { return mCount & 0x0000ffff; } ///< get count of trees on pixel
        public void increaseCount() { mCount++; } ///< increase the number of trees on pixel
        public bool isValid() { return Global.isBitSet(mCount, 16) == false; } ///< a value of 1: not valid (returns false)
        public void setValid(bool valid) { Global.setBit(ref mCount, 16, !valid); } ///< set bit to 1: pixel is not valid
        public bool isForestOutside() { return Global.isBitSet(mCount, 17); }
        public void setForestOutside(bool is_outside) { Global.setBit(ref mCount, 17, is_outside); }
        public bool isRadiating() { return Global.isBitSet(mCount, 18); }

        public void resetCount() { mCount &= unchecked((int)0xffff0000); } ///< set the count to 0
        public void setIsRadiating() { Global.setBit(ref mCount, 18, true); } ///< bit 18: if set, the pixel is actively radiating influence on the LIF (such pixels are on the edge of "forestOutside")
    }
}
