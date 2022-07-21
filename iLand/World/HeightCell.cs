namespace iLand.World
{
    public class HeightCell
    {
        private HeightCellFlags flags;

        public float MaximumVegetationHeightInM { get; set; } // height of tallest tree in cell, m, or height of regeneration layer
        public int TreeCount { get; private set; }

        public void AddTree(float height) 
        { 
            ++this.TreeCount;
            if (height > this.MaximumVegetationHeightInM)
            {
                this.MaximumVegetationHeightInM = height;
            }
        }

        public void ClearTrees()
        {
            this.MaximumVegetationHeightInM = Constant.RegenerationLayerHeight;
            this.TreeCount = 0;
        }

        public bool IsOnLandscape() 
        { 
            return this.flags.HasFlag(HeightCellFlags.OnLandscape); 
        }

        public bool IsRadiating() 
        { 
            return this.flags.HasFlag(HeightCellFlags.Radiating); 
        }

        public void SetIsRadiating() 
        {
            this.flags |= HeightCellFlags.Radiating;
        }

        public void SetOnLandscape(bool isOnLandscape)
        { 
            if (isOnLandscape)
            {
                this.flags |= HeightCellFlags.OnLandscape;
            }
            else
            {
                this.flags &= ~HeightCellFlags.OnLandscape;
            }
        }

        public override string ToString()
        {
            return this.TreeCount + " trees, " + this.MaximumVegetationHeightInM + " m, " + this.flags;
        }
    }
}
