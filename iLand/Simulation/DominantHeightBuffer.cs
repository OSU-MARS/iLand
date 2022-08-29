using iLand.World;

namespace iLand.Simulation
{
    public class DominantHeightBuffer : Grid<float>
    {
        public DominantHeightBuffer(bool isTorus)
            : base(isTorus ? Constant.Grid.HeightCellsPerRUWidth : Constant.Grid.DominantHeightFieldBufferWidthInHeightCells,
                   isTorus ? Constant.Grid.HeightCellsPerRUWidth : Constant.Grid.DominantHeightFieldBufferWidthInHeightCells,
                   Constant.Grid.HeightCellSizeInM)
        {
        }

        public void ApplyToHeightGrid(Grid<float> vegetationHeightGrid, int bufferLightOriginX, int bufferLightOriginY)
        {
            int bufferHeightOriginX = bufferLightOriginX / Constant.Grid.LightCellsPerHeightCellWidth;
            int bufferHeightOriginY = bufferLightOriginY / Constant.Grid.LightCellsPerHeightCellWidth;
            for (int bufferIndexY = 0; bufferIndexY < this.CellsY; ++bufferIndexY)
            {
                int heightIndex = vegetationHeightGrid.IndexXYToIndex(bufferHeightOriginX, bufferHeightOriginY + bufferIndexY);
                int bufferRowEndIndex = this.CellsY * (bufferIndexY + 1);
                for (int bufferRowIndex = this.CellsY * bufferIndexY; bufferRowIndex < bufferRowEndIndex; ++bufferRowIndex, ++heightIndex)
                {
                    float vegetationHeight = vegetationHeightGrid[heightIndex];
                    float dominantHeight = this[bufferRowIndex];
                    if (dominantHeight > vegetationHeight)
                    {
                        vegetationHeightGrid[heightIndex] = dominantHeight;
                    }
                }
            }
        }
    }
}
