using iLand.World;

namespace iLand.Simulation
{
    public class LightBuffer : Grid<float>
    {
        public LightBuffer(bool isTorus)
            : base(Constant.Grid.LightCellsPerRUWidth + (isTorus ? 0 : Constant.Grid.MaxLightStampSizeInLightCells),
                   Constant.Grid.LightCellsPerRUWidth + (isTorus ? 0 : Constant.Grid.MaxLightStampSizeInLightCells),
                   Constant.Grid.LightCellSizeInM)
        {
        }

        public void ApplyToLightGrid(Grid<float> lightGrid, int bufferLightOriginX, int bufferLightOriginY)
        {
            for (int bufferIndexY = 0; bufferIndexY < this.CellsY; ++bufferIndexY)
            {
                int lightIndex = lightGrid.IndexXYToIndex(bufferLightOriginX, bufferLightOriginY + bufferIndexY);
                int bufferRowEndIndex = this.CellsY * (bufferIndexY + 1);
                for (int bufferRowIndex = this.CellsY * bufferIndexY; bufferRowIndex < bufferRowEndIndex; ++bufferRowIndex, ++lightIndex)
                {
                    float lightGridIntensity = lightGrid[lightIndex];
                    float bufferShadingContribution = this[bufferRowIndex];
                    lightGrid[lightIndex] = bufferShadingContribution * lightGridIntensity;
                }
            }
        }
    }
}
