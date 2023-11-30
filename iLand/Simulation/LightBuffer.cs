using iLand.Extensions;
using iLand.World;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace iLand.Simulation
{
    public class LightBuffer : Grid<float>
    {
        public LightBuffer(bool isTorus)
            : base(Constant.Grid.LightCellsPerRUWidth + (isTorus ? 0 : Constant.Grid.MaxLightStampSizeInLightCells),
                   Constant.Grid.LightCellsPerRUWidth + (isTorus ? 0 : Constant.Grid.MaxLightStampSizeInLightCells),
                   Constant.Grid.LightCellSizeInM)
        {
            // not torus: 50 light cells + one half stamp width on either side of resource unit -> 114 x 114 cells
            //   114 = 28 * 128 bit SIMD + 2 scalar
            //       = 14 * 256 bit SIMD + 2 scalar
            // torus: 50 light cells -> 50 x 50 cells
            //   50 = 12 * 128 bit SIMD + 2 scalar
            //      = 6 * 256 bit SIMD + 2 scalar
            Debug.Assert((this.CellsX % Simd128.Width32 == 2) && (this.CellsX % Simd256.Width32 == 2));
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
                    lightGridIntensity = bufferShadingContribution * lightGridIntensity;
                    lightGrid[lightIndex] = lightGridIntensity;
                    // useful for checking correct memory addressing
                    // Debug.Assert((0.0F <= lightGridIntensity) && (lightGridIntensity <= 1.0F));
                }
            }
        }

        public unsafe void ApplyToLightGridVex128(Grid<float> lightGrid, int bufferLightOriginX, int bufferLightOriginY)
        {
            fixed (float* bufferCells = &this.Data[0], lightGridCells = &lightGrid.Data[0])
            {
                for (int bufferIndexY = 0; bufferIndexY < this.CellsY; ++bufferIndexY)
                {
                    // SIMD copy of majority of row
                    int lightIndex = lightGrid.IndexXYToIndex(bufferLightOriginX, bufferLightOriginY + bufferIndexY);
                    float* lightGridAddress = lightGridCells + lightIndex;
                    int bufferRowStartIndex = this.CellsX * bufferIndexY;
                    float* bufferRowEndAddress = bufferCells + bufferRowStartIndex + this.CellsX;
                    float* bufferRowEndAddressSimd = bufferRowEndAddress - Simd128.Width32; // ensure last SIMD copy doesn't go past end of buffer
                    for (float *bufferAddress = bufferCells + bufferRowStartIndex; bufferAddress < bufferRowEndAddressSimd; bufferAddress += Simd128.Width32, lightGridAddress += Simd128.Width32)
                    {
                        Vector128<float> lightGridIntensity = Avx.LoadVector128(lightGridAddress);
                        Vector128<float> patchShadingContribution = Avx.LoadVector128(bufferAddress);
                        lightGridIntensity = Avx.Multiply(patchShadingContribution, lightGridIntensity);
                        Avx.Store(lightGridAddress, lightGridIntensity);
                        // useful for checking correct memory addressing
                        // DebugV.Assert(Avx.And(Avx.CompareGreaterThanOrEqual(lightGridIntensity, Vector128<float>.Zero), Avx.CompareLessThanOrEqual(lightGridIntensity, Avx2Extensions.BroadcastScalarToVector128(1.0F))));
                    }

                    // last two cells of row are scalar
                    // lightGridAddress is incremented into place by loop above before it tests buffer address
                    Debug.Assert(lightGridAddress - (lightGridCells + lightIndex) == bufferRowEndAddress - (bufferCells + bufferRowStartIndex) - 2);
                    for (float* bufferAddress = bufferRowEndAddress - 2; bufferAddress < bufferRowEndAddress; ++bufferAddress)
                    {
                        float lightGridIntensity = *lightGridAddress;
                        float bufferShadingContribution = *bufferAddress;
                        lightGridIntensity *= bufferShadingContribution;
                        *lightGridAddress = lightGridIntensity;
                        // useful for checking correct memory addressing
                        // Debug.Assert((0.0F <= lightGridIntensity) && (lightGridIntensity <= 1.0F));
                    }
                }
            }
        }

        public unsafe void ApplyToLightGridAvx(Grid<float> lightGrid, int bufferLightOriginX, int bufferLightOriginY)
        {
            fixed (float* bufferCells = &this.Data[0], lightGridCells = &lightGrid.Data[0])
            {
                for (int bufferIndexY = 0; bufferIndexY < this.CellsY; ++bufferIndexY)
                {
                    // SIMD copy of majority of row
                    int lightIndex = lightGrid.IndexXYToIndex(bufferLightOriginX, bufferLightOriginY + bufferIndexY);
                    float* lightGridAddress = lightGridCells + lightIndex;
                    int bufferRowStartIndex = this.CellsX * bufferIndexY;
                    float* bufferRowEndAddress = bufferCells + bufferRowStartIndex + this.CellsX - Simd256.Width32;
                    float* bufferRowEndAddressSimd = bufferRowEndAddress - Simd256.Width32; // ensure last SIMD copy doesn't go past end of buffer
                    for (float* bufferAddress = bufferCells + bufferRowStartIndex; bufferAddress < bufferRowEndAddressSimd; bufferAddress += Simd256.Width32, lightGridAddress += Simd256.Width32)
                    {
                        Vector256<float> lightGridIntensity = Avx.LoadVector256(lightGridAddress);
                        Vector256<float> patchShadingContribution = Avx.LoadVector256(bufferAddress);
                        lightGridIntensity = Avx.Multiply(patchShadingContribution, lightGridIntensity);
                        Avx.Store(lightGridAddress, lightGridIntensity);
                        // useful for checking correct memory addressing
                        // DebugV.Assert(Avx.And(Avx.CompareGreaterThanOrEqual(lightGridIntensity, Vector256<float>.Zero), Avx.CompareLessThanOrEqual(lightGridIntensity, Avx2Extensions.BroadcastScalarToVector256(1.0F))));
                    }

                    // last two cells of row are scalar
                    // lightGridAddress is incremented into place by loop above before it tests buffer address
                    Debug.Assert(lightGridAddress - (lightGridCells + lightIndex) == bufferRowEndAddress - (bufferCells + bufferRowStartIndex) - 2);
                    for (float* bufferAddress = bufferRowEndAddress - 2; bufferAddress < bufferRowEndAddress; ++bufferAddress)
                    {
                        float lightGridIntensity = *lightGridAddress;
                        float bufferShadingContribution = *bufferAddress;
                        lightGridIntensity *= bufferShadingContribution;
                        *lightGridAddress = lightGridIntensity;
                        // useful for checking correct memory addressing
                        // Debug.Assert((0.0F <= lightGridIntensity) && (lightGridIntensity <= 1.0F));
                    }
                }
            }
        }
    }
}
