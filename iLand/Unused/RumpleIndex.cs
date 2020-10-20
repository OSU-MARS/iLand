using iLand.Simulation;
using iLand.World;
using System;

namespace iLand.Tools
{
    internal class RumpleIndex
    {
        private readonly Grid<float> mRumpleGrid;
        private double mRumpleIndex;
        private int mLastYear;

        public RumpleIndex()
        {
            this.mLastYear = -1;
            this.mRumpleGrid = new Grid<float>();
        }

        public void Setup(Model model)
        {
            mRumpleGrid.Clear();
            if (model == null)
            {
                return;
            }

            // the rumple grid has the same dimensions as the resource unit grid (i.e. 100 meters)
            mRumpleGrid.Setup(model.ResourceUnitGrid.PhysicalExtent, model.ResourceUnitGrid.CellSize);
        }

        // return the rumple index for the full project area
        public Grid<float> RumpleGrid(Model model)
        {
            Value(model); /* calculate if necessary */
            return mRumpleGrid;
        }

        public void Calculate(Model model)
        {
            if (mRumpleGrid.IsEmpty())
            {
                Setup(model);
            }

            mRumpleGrid.Initialize(0.0F);
            Grid<HeightCell> hg = model.HeightGrid;

            // iterate over the resource units and calculate the rumple index / surface area for each resource unit
            HeightCell[] hgv_8 = new HeightCell[8]; // array holding pointers to height grid values (neighborhood)
            float[] heights = new float[9];  // array holding heights (8er neighborhood + center pixel)
            int total_valid_pixels = 0;
            float total_surface_area = 0.0F;
            for (int rg = 0; rg != mRumpleGrid.Count; ++rg)
            {
                int valid_pixels = 0;
                float surface_area_sum = 0.0F;
                GridRunner<HeightCell> runner = new GridRunner<HeightCell>(hg, mRumpleGrid.GetCellRect(mRumpleGrid.IndexOf(rg)));
                for (runner.MoveNext(); runner.IsValid(); runner.MoveNext())
                {
                    if (runner.Current.IsInWorld())
                    {
                        runner.Neighbors8(hgv_8);
                        bool valid = true;
                        int hp = 0;
                        heights[hp++] = runner.Current.Height;
                        // retrieve height values from the grid
                        for (int i = 0; i < 8; ++i)
                        {
                            heights[hp++] = hgv_8[i] != null ? hgv_8[i].Height : 0;
                            if (hgv_8[i] != null && !hgv_8[i].IsInWorld())
                            {
                                valid = false;
                            }
                            if (hgv_8[i] != null)
                            {
                                valid = false;
                            }
                        }
                        // calculate surface area only for cells which are (a) within the project area, and (b) all neighboring pixels are inside the forest area
                        if (valid)
                        {
                            valid_pixels++;
                            float surface_area = (float)CalculateSurfaceArea(heights, hg.CellSize);
                            surface_area_sum += surface_area;
                        }
                    }
                }
                if (valid_pixels > 0)
                {
                    float rumple_index = surface_area_sum / ((float)valid_pixels * hg.CellSize * hg.CellSize);
                    mRumpleGrid[rg] = rumple_index;
                    total_valid_pixels += valid_pixels;
                    total_surface_area += surface_area_sum;
                }
            }
            mRumpleIndex = 0.0;
            if (total_valid_pixels > 0)
            {
                float rumple_index = total_surface_area / ((float)total_valid_pixels * hg.CellSize * hg.CellSize);
                mRumpleIndex = rumple_index;
            }
            mLastYear = model.ModelSettings.CurrentYear;
        }

        public double Value(Model model, bool force_recalculate = false)
        {
            if (force_recalculate || mLastYear != model.ModelSettings.CurrentYear)
            {
                Calculate(model);
            }
            return mRumpleIndex;
        }

        public double SurfaceLength(float h1, float h2, float l)
        {
            return Math.Sqrt((h1 - h2) * (h1 - h2) + l * l);
        }

        public double HeronTriangleArea(float a, float b, float c)
        {
            float s = (a + b + c) / 2.0F;
            return Math.Sqrt(s * (s - a) * (s - b) * (s - c));
        }

        /// calculate the surface area of a pixel given its height value, the height of the 8 neigboring pixels, and the cellsize
        /// the algorithm is based on http://www.jennessent.com/downloads/WSB_32_3_Jenness.pdf
        private double CalculateSurfaceArea(float[] heights, float cellsize)
        {
            // values in the height array [0..8]: own height / north/east/west/south/ NE/NW/SE/SW
            // step 1: calculate length on 3d surface between all edges
            //   8(A) * 1(B) * 5(C)       <- 0: center cell, indices in the "heights" grid, A..I: codes used by Jenness
            //   4(D) * 0(E) * 2(F)
            //   7(G) * 3(H) * 6(I)

            float[] slen = new float[16]; // surface lengths (divided by 2)
                                          // horizontal
            slen[0] = (float)SurfaceLength(heights[8], heights[1], cellsize) / 2.0F;
            slen[1] = (float)SurfaceLength(heights[1], heights[5], cellsize) / 2.0F;
            slen[2] = (float)SurfaceLength(heights[4], heights[0], cellsize) / 2.0F;
            slen[3] = (float)SurfaceLength(heights[0], heights[2], cellsize) / 2.0F;
            slen[4] = (float)SurfaceLength(heights[7], heights[3], cellsize) / 2.0F;
            slen[5] = (float)SurfaceLength(heights[3], heights[6], cellsize) / 2.0F;
            // vertical
            slen[6] = (float)SurfaceLength(heights[8], heights[4], cellsize) / 2.0F;
            slen[7] = (float)SurfaceLength(heights[1], heights[0], cellsize) / 2.0F;
            slen[8] = (float)SurfaceLength(heights[5], heights[2], cellsize) / 2.0F;
            slen[9] = (float)SurfaceLength(heights[4], heights[7], cellsize) / 2.0F;
            slen[10] = (float)SurfaceLength(heights[0], heights[3], cellsize) / 2.0F;
            slen[11] = (float)SurfaceLength(heights[2], heights[6], cellsize) / 2.0F;
            // diagonal
            float cellsize_diag = (float)Constant.Sqrt2 * cellsize;
            slen[12] = (float)SurfaceLength(heights[0], heights[8], cellsize_diag) / 2.0F;
            slen[13] = (float)SurfaceLength(heights[0], heights[5], cellsize_diag) / 2.0F;
            slen[14] = (float)SurfaceLength(heights[0], heights[7], cellsize_diag) / 2.0F;
            slen[15] = (float)SurfaceLength(heights[0], heights[6], cellsize_diag) / 2.0F;

            // step 2: combine the three sides of all the 8 sub triangles using Heron's formula
            double surface_area = 0.0;
            surface_area += HeronTriangleArea(slen[12], slen[0], slen[7]); // i
            surface_area += HeronTriangleArea(slen[7], slen[1], slen[13]); // ii
            surface_area += HeronTriangleArea(slen[6], slen[2], slen[12]); // iii
            surface_area += HeronTriangleArea(slen[13], slen[8], slen[3]); // iv
            surface_area += HeronTriangleArea(slen[2], slen[9], slen[14]); // v
            surface_area += HeronTriangleArea(slen[3], slen[11], slen[15]); // vi
            surface_area += HeronTriangleArea(slen[14], slen[10], slen[4]); // vii
            surface_area += HeronTriangleArea(slen[10], slen[15], slen[5]); // viii

            return surface_area;
        }
    }
}
