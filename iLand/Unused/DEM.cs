using iLand.Tool;
using System;
using System.Drawing;
using System.IO;

namespace iLand.World
{
    /** DEM is a digital elevation model class.
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

        public DEM(Landscape landscape, string demFilePath)
        {
            this.aspectGrid = new Grid<float>();
            this.slopeGrid = new Grid<float>();
            this.viewGrid = new Grid<float>();

            this.LoadFromFile(landscape, demFilePath);
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
        private bool LoadFromFile(Landscape landscape, string demFilePath)
        {
            if ((landscape == null) || (landscape.HeightGrid == null) || (landscape.HeightGrid.IsNotSetup()))
            {
                throw new ArgumentNullException(nameof(landscape), "No height grid available.");
            }

            Grid<HeightCell> heightGrid = landscape.HeightGrid;
            GisGrid demGrid = new();
            if (demGrid.LoadFromFile(demFilePath) == false)
            {
                throw new FileLoadException("Unable to load DEM file " + demFilePath + ".");
            }
            // create a grid with the same size as the height grid
            // (height-grid: 10m size, covering the full extent)
            this.Clear();
            this.aspectGrid.Clear();
            this.slopeGrid.Clear();
            this.viewGrid.Clear();

            this.Setup(heightGrid.PhysicalExtent, heightGrid.CellSize);

            if ((demGrid.CellSize % this.CellSize) != 0.0F)
            {
                // simple copy of the data
                for (int demCellIndex = 0; demCellIndex < this.Count; ++demCellIndex)
                {
                    PointF cellCenter = this.GetCellCenterPosition(this.GetCellPosition(demCellIndex));
                    float elevation = demGrid.GetValue(cellCenter);
                    if ((elevation != demGrid.NoDataValue) && landscape.Extent.Contains(cellCenter))
                    {
                        this[demCellIndex] = elevation;
                    }
                    else
                    {
                        this[demCellIndex] = -1;
                    }
                }
            }
            else
            {
                // bilinear approximation approach
                // Debug.WriteLine("DEM: built-in bilinear interpolation from cell size " + demGrid.CellSize);
                int sizeFactor = (int)(demGrid.CellSize / this.CellSize); // size-factor
                this.Fill(-1.0F);
                int ixmin = 10000000, iymin = 1000000, ixmax = -1, iymax = -1;
                for (int y = 0; y < demGrid.Rows; ++y)
                {
                    for (int x = 0; x < demGrid.Columns; ++x)
                    {
                        Vector3D p3d = demGrid.GetCoordinate(x, y);
                        if (landscape.Extent.Contains(p3d.X, p3d.Y))
                        {
                            Point pt = GetCellIndex(new PointF(p3d.X, p3d.Y));
                            this[p3d.X, p3d.Y] = demGrid.GetValue(x, y);
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
                                this[x + mx, y + my] = DEM.InterpolateBilinear(mx / sizeFactor, my / sizeFactor, c00, c10, c01, c11);
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
            Point pt = GetCellIndex(point);
            if (pt.X > 0 && pt.X < SizeX + 1 && pt.Y > 0 && pt.Y < SizeY - 1)
            {
                int p = this.IndexOf(pt);
                float z2 = this[p - SizeX];
                float z4 = this[p - 1];
                float z6 = this[p + 1];
                float z8 = this[p + SizeX];
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
            if (slopeGrid.IsNotSetup())
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
                PointF pt = GetCellCenterPosition(p);
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
        private static float InterpolateBilinear(float tx, float ty, float c00, float c10, float c01, float c11)
        {
            float a = c00 * (1.0F - tx) + c10 * tx;
            float b = c01 * (1.0F - tx) + c11 * tx;
            return a * (1.0F - ty) + b * ty;
        }
    }
}
