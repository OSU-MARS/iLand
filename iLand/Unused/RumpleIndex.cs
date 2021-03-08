using iLand.Simulation;
using iLand.World;
using System;

namespace iLand.Tools
{
    internal class RumpleIndex
    {
        private readonly Grid<float> mRumpleGrid;
        private float mRumpleIndex;
        private int mLastYear;

        public RumpleIndex()
        {
            this.mLastYear = -1;
            this.mRumpleGrid = new Grid<float>();
        }

        public void Setup(Model model)
        {
            this.mRumpleGrid.Clear();
            // the rumple grid has the same dimensions as the resource unit grid (i.e. 100 meters)
            this.mRumpleGrid.Setup(model.Landscape.ResourceUnitGrid.PhysicalExtent, model.Landscape.ResourceUnitGrid.CellSize);
        }

        // return the rumple index for the full project area
        public Grid<float> GetRumpleGrid(Model model)
        {
            this.GetIndex(model); /* calculate if necessary */
            return this.mRumpleGrid;
        }

        public void Calculate(Model model)
        {
            if (this.mRumpleGrid.IsNotSetup())
            {
                this.Setup(model);
            }

            this.mRumpleGrid.Fill(0.0F);
            Grid<HeightCell> heightGrid = model.Landscape.HeightGrid;

            // iterate over the resource units and calculate the rumple index / surface area for each resource unit
            HeightCell[] neighboringHeightCells = new HeightCell[8]; // array holding pointers to height grid values (neighborhood)
            float[] heights = new float[9];  // array holding heights (8er neighborhood + center pixel)
            int totalValidPixels = 0;
            float totalSurfaceArea = 0.0F;
            for (int rumpleGridIndex = 0; rumpleGridIndex != mRumpleGrid.Count; ++rumpleGridIndex)
            {
                int validPixels = 0;
                float surfaceAreaSum = 0.0F;
                GridWindowEnumerator<HeightCell> runner = new(heightGrid, mRumpleGrid.GetCellExtent(mRumpleGrid.GetCellPosition(rumpleGridIndex)));
                while (runner.MoveNext())
                {
                    if (runner.Current.IsOnLandscape())
                    {
                        runner.GetNeighbors8(neighboringHeightCells);
                        bool valid = true;
                        int hp = 0;
                        heights[hp++] = runner.Current.Height;
                        // retrieve height values from the grid
                        for (int i = 0; i < 8; ++i)
                        {
                            heights[hp++] = neighboringHeightCells[i] != null ? neighboringHeightCells[i].Height : 0;
                            if (neighboringHeightCells[i] != null && !neighboringHeightCells[i].IsOnLandscape())
                            {
                                valid = false;
                            }
                            if (neighboringHeightCells[i] != null)
                            {
                                valid = false;
                            }
                        }
                        // calculate surface area only for cells which are (a) within the project area, and (b) all neighboring pixels are inside the forest area
                        if (valid)
                        {
                            validPixels++;
                            float surface_area = RumpleIndex.CalculateSurfaceArea(heights, heightGrid.CellSize);
                            surfaceAreaSum += surface_area;
                        }
                    }
                }
                if (validPixels > 0)
                {
                    float rumpleIndex = surfaceAreaSum / (validPixels * heightGrid.CellSize * heightGrid.CellSize);
                    this.mRumpleGrid[rumpleGridIndex] = rumpleIndex;
                    totalValidPixels += validPixels;
                    totalSurfaceArea += surfaceAreaSum;
                }
            }
            this.mRumpleIndex = 0.0F;
            if (totalValidPixels > 0)
            {
                float rumpleIndex = totalSurfaceArea / (totalValidPixels * heightGrid.CellSize * heightGrid.CellSize);
                this.mRumpleIndex = rumpleIndex;
            }
            this.mLastYear = model.CurrentYear;
        }

        public float GetIndex(Model model, bool forceRecalculate = false)
        {
            if (forceRecalculate || this.mLastYear != model.CurrentYear)
            {
                this.Calculate(model);
            }
            return this.mRumpleIndex;
        }

        private static float SurfaceLength(float h1, float h2, float l)
        {
            return MathF.Sqrt((h1 - h2) * (h1 - h2) + l * l);
        }

        private static float HeronTriangleArea(float a, float b, float c)
        {
            float s = (a + b + c) / 2.0F;
            return MathF.Sqrt(s * (s - a) * (s - b) * (s - c));
        }

        /// calculate the surface area of a pixel given its height value, the height of the 8 neigboring pixels, and the cellsize
        /// the algorithm is based on http://www.jennessent.com/downloads/WSB_32_3_Jenness.pdf
        private static float CalculateSurfaceArea(float[] heights, float cellsize)
        {
            // values in the height array [0..8]: own height / north/east/west/south/ NE/NW/SE/SW
            // step 1: calculate length on 3d surface between all edges
            //   8(A) * 1(B) * 5(C)       <- 0: center cell, indices in the "heights" grid, A..I: codes used by Jenness
            //   4(D) * 0(E) * 2(F)
            //   7(G) * 3(H) * 6(I)

            float[] slen = new float[16]; // surface lengths (divided by 2)
                                          // horizontal
            slen[0] = RumpleIndex.SurfaceLength(heights[8], heights[1], cellsize) / 2.0F;
            slen[1] = RumpleIndex.SurfaceLength(heights[1], heights[5], cellsize) / 2.0F;
            slen[2] = RumpleIndex.SurfaceLength(heights[4], heights[0], cellsize) / 2.0F;
            slen[3] = RumpleIndex.SurfaceLength(heights[0], heights[2], cellsize) / 2.0F;
            slen[4] = RumpleIndex.SurfaceLength(heights[7], heights[3], cellsize) / 2.0F;
            slen[5] = RumpleIndex.SurfaceLength(heights[3], heights[6], cellsize) / 2.0F;
            // vertical
            slen[6] = RumpleIndex.SurfaceLength(heights[8], heights[4], cellsize) / 2.0F;
            slen[7] = RumpleIndex.SurfaceLength(heights[1], heights[0], cellsize) / 2.0F;
            slen[8] = RumpleIndex.SurfaceLength(heights[5], heights[2], cellsize) / 2.0F;
            slen[9] = RumpleIndex.SurfaceLength(heights[4], heights[7], cellsize) / 2.0F;
            slen[10] = RumpleIndex.SurfaceLength(heights[0], heights[3], cellsize) / 2.0F;
            slen[11] = RumpleIndex.SurfaceLength(heights[2], heights[6], cellsize) / 2.0F;
            // diagonal
            float cellsize_diag = Constant.Sqrt2 * cellsize;
            slen[12] = RumpleIndex.SurfaceLength(heights[0], heights[8], cellsize_diag) / 2.0F;
            slen[13] = RumpleIndex.SurfaceLength(heights[0], heights[5], cellsize_diag) / 2.0F;
            slen[14] = RumpleIndex.SurfaceLength(heights[0], heights[7], cellsize_diag) / 2.0F;
            slen[15] = RumpleIndex.SurfaceLength(heights[0], heights[6], cellsize_diag) / 2.0F;

            // step 2: combine the three sides of all the 8 sub triangles using Heron's formula
            float surface_area = 0.0F;
            surface_area += RumpleIndex.HeronTriangleArea(slen[12], slen[0], slen[7]); // i
            surface_area += RumpleIndex.HeronTriangleArea(slen[7], slen[1], slen[13]); // ii
            surface_area += RumpleIndex.HeronTriangleArea(slen[6], slen[2], slen[12]); // iii
            surface_area += RumpleIndex.HeronTriangleArea(slen[13], slen[8], slen[3]); // iv
            surface_area += RumpleIndex.HeronTriangleArea(slen[2], slen[9], slen[14]); // v
            surface_area += RumpleIndex.HeronTriangleArea(slen[3], slen[11], slen[15]); // vi
            surface_area += RumpleIndex.HeronTriangleArea(slen[14], slen[10], slen[4]); // vii
            surface_area += RumpleIndex.HeronTriangleArea(slen[10], slen[15], slen[5]); // viii

            return surface_area;
        }
    }
}
