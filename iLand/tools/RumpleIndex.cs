using iLand.core;
using System;

namespace iLand.tools
{
    internal class RumpleIndex
    {
        private Grid<float> mRumpleGrid;
        private double mRumpleIndex;
        private int mLastYear;

        public RumpleIndex()
        {
            mLastYear = -1;
        }

        public void setup()
        {
            mRumpleGrid.clear();
            if (GlobalSettings.instance().model() == null)
            {
                return;
            }

            // the rumple grid hast the same dimensions as the resource unit grid (i.e. 100 meters)
            mRumpleGrid.setup(GlobalSettings.instance().model().RUgrid().metricRect(),
                              GlobalSettings.instance().model().RUgrid().cellsize());
        }

        ///< return the rumple index for the full project area
        public Grid<float> rumpleGrid()
        {
            value(); /* calculate if necessary */
            return mRumpleGrid;
        }

        public void calculate()
        {
            if (mRumpleGrid.isEmpty())
            {
                setup();
            }

            mRumpleGrid.initialize(0.0F);
            Grid<HeightGridValue> hg = GlobalSettings.instance().model().heightGrid();

            // iterate over the resource units and calculate the rumple index / surface area for each resource unit
            HeightGridValue[] hgv_8 = new HeightGridValue[8]; // array holding pointers to height grid values (neighborhood)
            float[] heights = new float[9];  // array holding heights (8er neighborhood + center pixel)
            int total_valid_pixels = 0;
            float total_surface_area = 0.0F;
            for (int rg = 0; rg != mRumpleGrid.count(); ++rg)
            {
                int valid_pixels = 0;
                float surface_area_sum = 0.0F;
                GridRunner<HeightGridValue> runner = new GridRunner<HeightGridValue>(hg, mRumpleGrid.cellRect(mRumpleGrid.indexOf(rg)));
                for (runner.next(); runner.isValid(); runner.next())
                {
                    if (runner.current().isValid())
                    {
                        runner.neighbors8(hgv_8);
                        bool valid = true;
                        int hp = 0;
                        heights[hp++] = runner.current().height;
                        // retrieve height values from the grid
                        for (int i = 0; i < 8; ++i)
                        {
                            heights[hp++] = hgv_8[i] != null ? hgv_8[i].height : 0;
                            if (hgv_8[i] != null && !hgv_8[i].isValid())
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
                            float surface_area = (float)calculateSurfaceArea(heights, hg.cellsize());
                            surface_area_sum += surface_area;
                        }
                    }
                }
                if (valid_pixels > 0)
                {
                    float rumple_index = surface_area_sum / ((float)valid_pixels * hg.cellsize() * hg.cellsize());
                    mRumpleGrid[rg] = rumple_index;
                    total_valid_pixels += valid_pixels;
                    total_surface_area += surface_area_sum;
                }
            }
            mRumpleIndex = 0.0;
            if (total_valid_pixels > 0)
            {
                float rumple_index = total_surface_area / ((float)total_valid_pixels * hg.cellsize() * hg.cellsize());
                mRumpleIndex = rumple_index;
            }
            mLastYear = GlobalSettings.instance().currentYear();
        }

        public double value(bool force_recalculate = false)
        {
            if (force_recalculate || mLastYear != GlobalSettings.instance().currentYear())
            {
                calculate();
            }
            return mRumpleIndex;
        }

        public double test_triangle_area()
        {
            // check calculation: numbers for Jenness paper
            float[] hs = new float[] { 165, 170, 145, 160, 183, 155, 122, 175, 190 };
            double area = calculateSurfaceArea(hs, 100);
            return area;
        }

        public double surface_length(float h1, float h2, float l)
        {
            return Math.Sqrt((h1 - h2) * (h1 - h2) + l * l);
        }

        public double heron_triangle_area(float a, float b, float c)
        {
            float s = (a + b + c) / 2.0F;
            return Math.Sqrt(s * (s - a) * (s - b) * (s - c));
        }

        /// calculate the surface area of a pixel given its height value, the height of the 8 neigboring pixels, and the cellsize
        /// the algorithm is based on http://www.jennessent.com/downloads/WSB_32_3_Jenness.pdf
        private double calculateSurfaceArea(float[] heights, float cellsize)
        {
            // values in the height array [0..8]: own height / north/east/west/south/ NE/NW/SE/SW
            // step 1: calculate length on 3d surface between all edges
            //   8(A) * 1(B) * 5(C)       <- 0: center cell, indices in the "heights" grid, A..I: codes used by Jenness
            //   4(D) * 0(E) * 2(F)
            //   7(G) * 3(H) * 6(I)

            float[] slen = new float[16]; // surface lengths (divided by 2)
                                          // horizontal
            slen[0] = (float)surface_length(heights[8], heights[1], cellsize) / 2.0F;
            slen[1] = (float)surface_length(heights[1], heights[5], cellsize) / 2.0F;
            slen[2] = (float)surface_length(heights[4], heights[0], cellsize) / 2.0F;
            slen[3] = (float)surface_length(heights[0], heights[2], cellsize) / 2.0F;
            slen[4] = (float)surface_length(heights[7], heights[3], cellsize) / 2.0F;
            slen[5] = (float)surface_length(heights[3], heights[6], cellsize) / 2.0F;
            // vertical
            slen[6] = (float)surface_length(heights[8], heights[4], cellsize) / 2.0F;
            slen[7] = (float)surface_length(heights[1], heights[0], cellsize) / 2.0F;
            slen[8] = (float)surface_length(heights[5], heights[2], cellsize) / 2.0F;
            slen[9] = (float)surface_length(heights[4], heights[7], cellsize) / 2.0F;
            slen[10] = (float)surface_length(heights[0], heights[3], cellsize) / 2.0F;
            slen[11] = (float)surface_length(heights[2], heights[6], cellsize) / 2.0F;
            // diagonal
            float cellsize_diag = (float)Constant.M_SQRT2 * cellsize;
            slen[12] = (float)surface_length(heights[0], heights[8], cellsize_diag) / 2.0F;
            slen[13] = (float)surface_length(heights[0], heights[5], cellsize_diag) / 2.0F;
            slen[14] = (float)surface_length(heights[0], heights[7], cellsize_diag) / 2.0F;
            slen[15] = (float)surface_length(heights[0], heights[6], cellsize_diag) / 2.0F;

            // step 2: combine the three sides of all the 8 sub triangles using Heron's formula
            double surface_area = 0.0;
            surface_area += heron_triangle_area(slen[12], slen[0], slen[7]); // i
            surface_area += heron_triangle_area(slen[7], slen[1], slen[13]); // ii
            surface_area += heron_triangle_area(slen[6], slen[2], slen[12]); // iii
            surface_area += heron_triangle_area(slen[13], slen[8], slen[3]); // iv
            surface_area += heron_triangle_area(slen[2], slen[9], slen[14]); // v
            surface_area += heron_triangle_area(slen[3], slen[11], slen[15]); // vi
            surface_area += heron_triangle_area(slen[14], slen[10], slen[4]); // vii
            surface_area += heron_triangle_area(slen[10], slen[15], slen[5]); // viii

            return surface_area;
        }
    }
}
