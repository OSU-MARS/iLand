using iLand.core;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;

namespace iLand.tools
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
    internal class DEM : Grid<float>
    {
        private Grid<float> aspect_grid;
        private Grid<float> slope_grid;
        private Grid<float> view_grid;

        public Grid<float> aspectGrid()
        {
            createSlopeGrid();
            return aspect_grid;
        }

        public Grid<float> slopeGrid()
        {
            createSlopeGrid();
            return slope_grid;
        }

        public Grid<float> viewGrid()
        {
            createSlopeGrid();
            return view_grid;
        }

        // special functions for DEM
        /// get the elevation (m) at point (x/y)
        public float elevation(float x, float y)
        {
            return constValueAt(x, y);
        }

        public float elevation(PointF p)
        {
            return constValueAt(p.X, p.Y);
        }

        public DEM(string fileName)
        {
            loadFromFile(fileName);
        }

        public float orientation(float x, float y, float rslope_angle, float rslope_aspect)
        {
            return orientation(new PointF(x, y), rslope_angle, rslope_aspect);
        }

        /// loads a DEM from a ESRI style text file.
        /// internally, the DEM has always a resolution of 10m
        public bool loadFromFile(string fileName)
        {
            if (GlobalSettings.instance().model() == null)
            {
                throw new NotSupportedException("create10mGrid: no valid model to retrieve height grid.");
            }

            Grid<HeightGridValue> h_grid = GlobalSettings.instance().model().heightGrid();
            if (h_grid == null || h_grid.isEmpty())
            {
                throw new NotSupportedException("GisGrid::create10mGrid: no valid height grid to copy grid size.");
            }

            GisGrid gis_grid = new GisGrid();
            if (!gis_grid.loadFromFile(fileName))
            {
                throw new FileLoadException(String.Format("Unable to load DEM file {0}", fileName));
            }
            // create a grid with the same size as the height grid
            // (height-grid: 10m size, covering the full extent)
            clear();
            aspect_grid.clear();
            slope_grid.clear();
            view_grid.clear();

            setup(h_grid.metricRect(), h_grid.cellsize());

            RectangleF world = GlobalSettings.instance().model().extent();

            if ((gis_grid.cellSize() % cellsize()) != 0.0)
            {
                PointF p;
                // simple copy of the data
                for (int i = 0; i < count(); i++)
                {
                    p = cellCenterPoint(indexOf(i));
                    if (gis_grid.value(p) != gis_grid.noDataValue() && world.Contains(p))
                    {
                        this[i] = (float)gis_grid.value(p);
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
                Debug.WriteLine("DEM: built-in bilinear interpolation from cell size " + gis_grid.cellSize());
                int f = (int)(gis_grid.cellSize() / cellsize()); // size-factor
                initialize(-1.0F);
                int ixmin = 10000000, iymin = 1000000, ixmax = -1, iymax = -1;
                for (int y = 0; y < gis_grid.rows(); ++y)
                {
                    for (int x = 0; x < gis_grid.cols(); ++x)
                    {
                        Vector3D p3d = gis_grid.coord(x, y);
                        if (world.Contains((float)p3d.x(), (float)p3d.y()))
                        {
                            Point pt = indexAt(new PointF((float)p3d.x(), (float)p3d.y()));
                            this[(float)p3d.x(), (float)p3d.y()] = (float)gis_grid.value(x, y);
                            ixmin = Math.Min(ixmin, pt.X); ixmax = Math.Max(ixmax, pt.X);
                            iymin = Math.Min(iymin, pt.Y); iymax = Math.Max(iymax, pt.Y);
                        }
                    }
                }

                for (int y = iymin; y <= iymax - f; y += f)
                {
                    for (int x = ixmin; x <= ixmax - f; x += f)
                    {
                        float c00 = valueAtIndex(x, y);
                        float c10 = valueAtIndex(x + f, y);
                        float c01 = valueAtIndex(x, y + f);
                        float c11 = valueAtIndex(x + f, y + f);
                        for (int my = 0; my < f; ++my)
                        {
                            for (int mx = 0; mx < f; ++mx)
                            {
                                this[x + mx, y + my] = bilinear(mx / (float)f, my / (float)f, c00, c10, c01, c11);
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
        public float orientation(PointF point, float rslope_angle, float rslope_aspect)
        {
            Point pt = indexAt(point);
            if (pt.X > 0 && pt.X < sizeX() + 1 && pt.Y > 0 && pt.Y < sizeY() - 1)
            {
                int p = this.index(pt);
                float z2 = this[p - sizeX()];
                float z4 = this[p - 1];
                float z6 = this[p + 1];
                float z8 = this[p + sizeX()];
                float g = (-z4 + z6) / (2 * cellsize());
                float h = (z2 - z8) / (2 * cellsize());

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
                    aspect = aspect % 360.0F;
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

        public void createSlopeGrid()
        {
            if (slope_grid.isEmpty())
            {
                // setup custom grids with the same size as this DEM
                slope_grid.setup(this);
                view_grid.setup(this);
                aspect_grid.setup(this);
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
            for (int p = 0; p < this.count(); ++p)
            {
                PointF pt = cellCenterPoint(p);
                float height = orientation(pt, slope_grid[p], aspect_grid[p]);
                // calculate the view value:
                if (height > 0)
                {
                    float h = MathF.Atan(slope_grid[p]);
                    a_x = MathF.Cos(aspect_grid[p] * MathF.PI / 180.0F) * MathF.Cos(h);
                    a_y = MathF.Sin(aspect_grid[p] * MathF.PI / 180.0F) * MathF.Cos(h);
                    a_z = MathF.Sin(h);

                    // use the scalar product to calculate the angle, and then
                    // transform from [-1,1] to [0,1]
                    view_grid[p] = (a_x * sun_x + a_y * sun_y + a_z * sun_z + 1.0F) / 2.0F;
                }
                else
                {
                    view_grid[p] = 0.0F;
                }
            }
        }

        // from here: http://www.scratchapixel.com/lessons/3d-advanced-lessons/interpolation/bilinear-interpolation/
        private float bilinear(float tx, float ty, float c00, float c10, float c01, float c11)
        {
            float a = c00 * (1.0F - tx) + c10 * tx;
            float b = c01 * (1.0F - tx) + c11 * tx;
            return a * (1.0F - ty) + b * ty;
        }
    }
}
