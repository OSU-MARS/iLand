using iLand.Simulation;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace iLand.World
{
    /** DEM is a digital elevation model class.
     @ingroup tools
      It uses a float grid internally.
      slope is calculated in "%", i.e. a value of 1 is 45° (90° -> inf)

      The aspect angles are defined as follows (like ArcGIS):
             0°
             N
      270° W x E 90°
             S
            180°

      Values for height of -1 indicate "out of scope", "invalid" values
     */
    public class DEM : Grid<float>
    {
        private readonly Grid<float> aspectGrid;
        private readonly Grid<float> slopeGrid;
        private readonly Grid<float> viewGrid;

        public DEM(string fileName, Model model)
        {
            this.aspectGrid = new Grid<float>();
            this.slopeGrid = new Grid<float>();
            this.viewGrid = new Grid<float>();

            this.LoadFromFile(fileName, model);
        }

        public Grid<float> EnsureAspectGrid()
        {
            CreateGrids();
            return aspectGrid;
        }

        public Grid<float> EnsureSlopeGrid()
        {
            CreateGrids();
            return slopeGrid;
        }

        public Grid<float> EnsureViewGrid()
        {
            CreateGrids();
            return viewGrid;
        }

        // special functions for DEM
        /// get the elevation (m) at point (x/y)
        public float GetElevation(float x, float y)
        {
            return this[x, y];
        }

        public float GetElevation(PointF p)
        {
            return this[p];
        }

        public float GetOrientation(float x, float y, out float rslope_angle, out float rslope_aspect)
        {
            return GetOrientation(new PointF(x, y), out rslope_angle, out rslope_aspect);
        }

        /// loads a DEM from a ESRI style text file.
        /// internally, the DEM has always a resolution of 10m
        public bool LoadFromFile(string fileName, Model model)
        {
            if (model == null)
            {
                throw new NotSupportedException("No valid model to retrieve height grid.");
            }

            Grid<HeightCell> h_grid = model.HeightGrid;
            if (h_grid == null || h_grid.IsEmpty())
            {
                throw new NotSupportedException("GisGrid::create10mGrid: no valid height grid to copy grid size.");
            }

            GisGrid gis_grid = new GisGrid();
            if (!gis_grid.LoadFromFile(fileName))
            {
                throw new FileLoadException(String.Format("Unable to load DEM file {0}", fileName));
            }
            // create a grid with the same size as the height grid
            // (height-grid: 10m size, covering the full extent)
            Clear();
            aspectGrid.Clear();
            slopeGrid.Clear();
            viewGrid.Clear();

            Setup(h_grid.PhysicalExtent, h_grid.CellSize);

            RectangleF world = model.WorldExtentUnbuffered;

            if ((gis_grid.CellSize % CellSize) != 0.0)
            {
                PointF p;
                // simple copy of the data
                for (int i = 0; i < Count; i++)
                {
                    p = GetCellCenterPoint(IndexOf(i));
                    if (gis_grid.GetValue(p) != gis_grid.NoDataValue && world.Contains(p))
                    {
                        this[i] = (float)gis_grid.GetValue(p);
                    }
                    else
                    {
                        this[i] = -1;
                    }
                }
            }
            else
            {
                // bilinear approximation approach
                Debug.WriteLine("DEM: built-in bilinear interpolation from cell size " + gis_grid.CellSize);
                int sizeFactor = (int)(gis_grid.CellSize / this.CellSize); // size-factor
                Initialize(-1.0F);
                int ixmin = 10000000, iymin = 1000000, ixmax = -1, iymax = -1;
                for (int y = 0; y < gis_grid.Rows; ++y)
                {
                    for (int x = 0; x < gis_grid.Cols; ++x)
                    {
                        Vector3D p3d = gis_grid.GetCoordinate(x, y);
                        if (world.Contains((float)p3d.X, (float)p3d.Y))
                        {
                            Point pt = IndexAt(new PointF((float)p3d.X, (float)p3d.Y));
                            this[(float)p3d.X, (float)p3d.Y] = (float)gis_grid.GetValue(x, y);
                            ixmin = Math.Min(ixmin, pt.X); ixmax = Math.Max(ixmax, pt.X);
                            iymin = Math.Min(iymin, pt.Y); iymax = Math.Max(iymax, pt.Y);
                        }
                    }
                }

                for (int y = iymin; y <= iymax - sizeFactor; y += sizeFactor)
                {
                    for (int x = ixmin; x <= ixmax - sizeFactor; x += sizeFactor)
                    {
                        float c00 = this[x, y];
                        float c10 = this[x + sizeFactor, y];
                        float c01 = this[x, y + sizeFactor];
                        float c11 = this[x + sizeFactor, y + sizeFactor];
                        for (int my = 0; my < sizeFactor; ++my)
                        {
                            for (int mx = 0; mx < sizeFactor; ++mx)
                            {
                                this[x + mx, y + my] = Bilinear(mx / (float)sizeFactor, my / (float)sizeFactor, c00, c10, c01, c11);
                            }
                        }
                    }
                }
            }

            return true;
        }

        /// calculate slope and aspect at a given point.
        /// results are params per reference.
        /// returns the height at point (x/y)
        /// calculation follows: Burrough, P. A. and McDonell, R.A., 1998.Principles of Geographical Information Systems.(Oxford University Press, New York), p. 190.
        /// http://uqu.edu.sa/files2/tiny_mce/plugins/filemanager/files/4280125/Principles%20of%20Geographical%20Information%20Systems.pdf
        /// @param point metric coordinates of point to derive orientation
        /// @param rslope_angle RESULTING (passed by reference) slope angle as percentage (i.e: 1:=45 degrees)
        /// @param rslope_aspect RESULTING slope direction in degrees (0: North, 90: east, 180: south, 270: west)
        public float GetOrientation(PointF point, out float rslope_angle, out float rslope_aspect)
        {
            Point pt = IndexAt(point);
            if (pt.X > 0 && pt.X < CellsX + 1 && pt.Y > 0 && pt.Y < CellsY - 1)
            {
                int p = this.IndexOf(pt);
                float z2 = this[p - CellsX];
                float z4 = this[p - 1];
                float z6 = this[p + 1];
                float z8 = this[p + CellsX];
                float g = (-z4 + z6) / (2 * CellSize);
                float h = (z2 - z8) / (2 * CellSize);

                if (z2 <= 0.0F || z4 <= 0.0F || z6 <= 0.0F || z8 <= 0)
                {
                    rslope_angle = 0.0F;
                    rslope_aspect = 0.0F;
                }
                else
                {
                    rslope_angle = MathF.Sqrt(g * g + h * h);
                    // atan2: returns -pi : +pi
                    // North: -pi/2, east: 0, south: +pi/2, west: -pi/+pi
                    float aspect = MathF.Atan2(-h, -g);
                    // transform to degree:
                    // north: 0, east: 90, south: 180, west: 270
                    aspect = aspect * 180.0F / MathF.PI + 360.0F + 90.0F;
                    aspect %= 360.0F;
                    rslope_aspect = aspect;
                }
                return this[p];
            }
            else
            {
                rslope_angle = 0.0F;
                rslope_aspect = 0.0F;
                return 0.0F;
            }
        }

        public void CreateGrids()
        {
            if (slopeGrid.IsEmpty())
            {
                // setup custom grids with the same size as this DEM
                slopeGrid.Setup(this);
                viewGrid.Setup(this);
                aspectGrid.Setup(this);
            }
            else
            {
                return;
            }

            // use fixed values for azimuth (315) and angle (45 deg) and calculate
            // norm vectors
            float sun_x = MathF.Cos(315.0F * MathF.PI / 180.0F) * MathF.Cos(45.0F * MathF.PI / 180.0F);
            float sun_y = MathF.Sin(315.0F * MathF.PI / 180.0F) * MathF.Cos(45.0F * MathF.PI / 180.0F);
            float sun_z = MathF.Sin(45.0F * MathF.PI / 180.0F);

            float a_x, a_y, a_z;
            for (int p = 0; p < this.Count; ++p)
            {
                PointF pt = GetCellCenterPoint(p);
                float height = GetOrientation(pt, out float slope, out float aspect);
                slopeGrid[p] = slope;
                aspectGrid[p] = aspect;
                // calculate the view value:
                if (height > 0)
                {
                    float h = MathF.Atan(slopeGrid[p]);
                    a_x = MathF.Cos(aspectGrid[p] * MathF.PI / 180.0F) * MathF.Cos(h);
                    a_y = MathF.Sin(aspectGrid[p] * MathF.PI / 180.0F) * MathF.Cos(h);
                    a_z = MathF.Sin(h);

                    // use the scalar product to calculate the angle, and then
                    // transform from [-1,1] to [0,1]
                    viewGrid[p] = (a_x * sun_x + a_y * sun_y + a_z * sun_z + 1.0F) / 2.0F;
                }
                else
                {
                    viewGrid[p] = 0.0F;
                }
            }
        }

        // from here: http://www.scratchapixel.com/lessons/3d-advanced-lessons/interpolation/bilinear-interpolation/
        private float Bilinear(float tx, float ty, float c00, float c10, float c01, float c11)
        {
            float a = c00 * (1.0F - tx) + c10 * tx;
            float b = c01 * (1.0F - tx) + c11 * tx;
            return a * (1.0F - ty) + b * ty;
        }
    }
}
