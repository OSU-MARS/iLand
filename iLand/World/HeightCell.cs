﻿namespace iLand.World
{
    public class HeightCell
    {
        private HeightCellFlags mFlags;

        // dominant tree height (m)
        public float Height { get; set; }

        //public void Init(float height, int count) 
        //{
        //    this.mFlagsTreeCount = count;
        //    this.Height = height; 
        //}

        // get count of trees on pixel
        public int TreeCount { get; private set; }

        public void AddTree(float height) 
        { 
            ++this.TreeCount;
            if (height > this.Height)
            {
                this.Height = height;
            }
        }

        public bool IsOnLandscape() { return this.mFlags.HasFlag(HeightCellFlags.OnLandscape); }
        public bool IsRadiating() { return this.mFlags.HasFlag(HeightCellFlags.Radiating); }

        // set the count to 0
        public void ResetTreeCount() 
        {
            this.TreeCount = 0;
        }

        public void SetIsRadiating() 
        {
            this.mFlags |= HeightCellFlags.Radiating;
        }

        public void SetInWorld(bool isInWorld)
        { 
            if (isInWorld)
            {
                this.mFlags |= HeightCellFlags.OnLandscape;
            }
            else
            {
                this.mFlags &= ~HeightCellFlags.OnLandscape;
            }
        }

        public override string ToString()
        {
            return this.Height + "," + this.TreeCount + "," + this.mFlags;
        }
    }
}
