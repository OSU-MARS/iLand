namespace iLand.Core
{
    public class HeightCell
    {
        private int mFlags;

        ///< dominant tree height (m)
        public float Height { get; set; }

        //public void Init(float height, int count) 
        //{
        //    this.mFlagsTreeCount = count;
        //    this.Height = height; 
        //}

        ///< get count of trees on pixel
        public int TreeCount { get; private set; }

        public void AddTree(float height) 
        { 
            ++this.TreeCount;
            if (height > this.Height)
            {
                this.Height = height;
            }
        }

        public bool IsInWorld() { return Global.IsBitSet(mFlags, 16) == false; } ///< a value of 1: not valid (returns false)
        public bool IsOutsideWorld() { return Global.IsBitSet(mFlags, 17); } // TODO: why is this not bit 16 == true?
        public bool IsRadiating() { return Global.IsBitSet(mFlags, 18); }

        ///< set the count to 0
        public void ResetTreeCount() 
        {
            this.TreeCount = 0;
        }

        public void SetIsOutsideWorld(bool isOutside) 
        { 
            Global.SetBit(ref mFlags, 17, isOutside); 
        }

        ///< bit 18: if set, the pixel is actively radiating influence on the LIF (such pixels are on the edge of "forestOutside")
        public void SetIsRadiating() 
        { 
            Global.SetBit(ref mFlags, 18, true); 
        }

        ///< set bit to 1: pixel is not valid
        public void SetInWorld(bool valid) 
        { 
            Global.SetBit(ref mFlags, 16, !valid); 
        }

        public override string ToString()
        {
            return this.Height + "," + this.TreeCount + "," + this.mFlags;
        }
    }
}
