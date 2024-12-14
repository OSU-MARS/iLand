// C++/tools/{ dem.h, dem.cpp }
using iLand.Tool;
using System;
using System.Drawing;

namespace iLand.World
{
    public class DigitalElevationModel : Grid<float>
    {
        private readonly Grid<float> slope_grid;
        private readonly Grid<float> view_grid;

        public Grid<float> AspectGrid { get; private init; }

        public DigitalElevationModel()
        {
            this.slope_grid = new();
            this.view_grid = new();

            this.AspectGrid = new();
        }

        /// loads a DEM from a ESRI style text file.
        /// internally, the DEM has always a resolution of 10m
        public void LoadFromFile(string demFilePath, Grid<float> vegetationHeightGrid)
        {
            if (vegetationHeightGrid.IsSetup())
            {
                throw new ArgumentOutOfRangeException(nameof(vegetationHeightGrid), "Can't determine digital elevation model extents as model's height grid has not been setup.");
            }

            GisGrid<float> gis_grid = new();
            gis_grid.LoadFromFile(demFilePath);
            if (gis_grid.Transform.CellHeight >= 0.0)
            {
                // constraint can be removed by linking index offsets to sign of cell height
                throw new NotSupportedException("Non-negative cell heights are not supported. Raster transform for digital elevation model'" + demFilePath + "' has cell height " + gis_grid.Transform.CellHeight + ".");
            }

            this.Setup(vegetationHeightGrid.ProjectExtent, vegetationHeightGrid.CellSizeInM);
            Array.Fill(this.Data, Single.NaN); // default all cells to no data

            //const QRectF &world = GlobalSettings::instance().model().extent(); // without buffer
            //RectangleF world = h_grid.ProjectExtent; // including buffer

            // bilinear interpolation of DEM values to height cell centers
            float gisGridCellHeight = (float)Double.Abs(gis_grid.Transform.CellHeight);
            float gisGridCellWidth = (float)gis_grid.Transform.CellWidth;
            float gisInverseCellArea = 1.0F / (gisGridCellWidth * gisGridCellHeight);
            for (int cellIndex = 0; cellIndex < this.CellCount; ++cellIndex)
            {
                PointF heightCellCentroid = this.GetCellProjectCentroid(cellIndex);
                float heightCellCentroidX = heightCellCentroid.X;
                float heightCellCentroidY = heightCellCentroid.Y;

                (int gisIndexX, int gisIndexY) = gis_grid.ToInteriorGridIndices(heightCellCentroidX, heightCellCentroidY);
                (double gisGridCellCentroidXasDouble, double gisGridCellCentroidYasDouble) = gis_grid.GetCellCentroid(gisIndexX, gisIndexY);
                float gisGridCellCentroidX = (float)gisGridCellCentroidXasDouble;
                float gisGridCellCentroidY = (float)gisGridCellCentroidYasDouble;

                float gisXwest;
                float gisXeast;
                float gisYnorth;
                float gisYsouth;
                float gisZnw;
                float gisZne;
                float gisZsw;
                float gisZse;
                if (heightCellCentroidY > gisGridCellCentroidY)
                {
                    gisYnorth = gisGridCellCentroidY + gisGridCellHeight;
                    gisYsouth = gisGridCellCentroidY;
                    if (heightCellCentroidX <= gisGridCellCentroidX)
                    {
                        // height cell is to northwest of GIS cell
                        gisXwest = gisGridCellCentroidX - gisGridCellWidth;
                        gisXeast = gisGridCellCentroidX;
                        gisZnw = gis_grid[gisIndexX - 1, gisIndexY - 1];
                        gisZne = gis_grid[gisIndexX, gisIndexY - 1];
                        gisZsw = gis_grid[gisIndexX - 1, gisIndexY];
                        gisZse = gis_grid[gisIndexX, gisIndexY];
                    }
                    else
                    {
                        // northeast
                        gisXwest = gisGridCellCentroidX;
                        gisXeast = gisGridCellCentroidX + gisGridCellWidth;
                        gisZnw = gis_grid[gisIndexX, gisIndexY - 1];
                        gisZne = gis_grid[gisIndexX + 1, gisIndexY - 1];
                        gisZsw = gis_grid[gisIndexX, gisIndexY];
                        gisZse = gis_grid[gisIndexX + 1, gisIndexY];
                    }
                }
                else
                {
                    gisYnorth = gisGridCellCentroidY;
                    gisYsouth = gisGridCellCentroidY - gisGridCellHeight;
                    if (heightCellCentroidX <= gisGridCellCentroidX)
                    {
                        // southwest
                        gisXwest = gisGridCellCentroidX - gisGridCellWidth;
                        gisXeast = gisGridCellCentroidX;
                        gisZnw = gis_grid[gisIndexX - 1, gisIndexY];
                        gisZne = gis_grid[gisIndexX, gisIndexY];
                        gisZsw = gis_grid[gisIndexX - 1, gisIndexY + 1];
                        gisZse = gis_grid[gisIndexX, gisIndexY + 1];
                    }
                    else
                    {
                        // southeast
                        gisXwest = gisGridCellCentroidX;
                        gisXeast = gisGridCellCentroidX + gisGridCellWidth;
                        gisZnw = gis_grid[gisIndexX, gisIndexY];
                        gisZne = gis_grid[gisIndexX + 1, gisIndexY];
                        gisZsw = gis_grid[gisIndexX, gisIndexY + 1];
                        gisZse = gis_grid[gisIndexX + 1, gisIndexY + 1];
                    }
                }

                if (gis_grid.IsNoData(gisZnw) || gis_grid.IsNoData(gisZne) || gis_grid.IsNoData(gisZsw) || gis_grid.IsNoData(gisZse))
                {
                    throw new NotSupportedException("Digital elevation model '" + demFilePath + " contains one or more no data values in cells adjacent to (" + heightCellCentroidX + ", " + heightCellCentroidY + ").");
                }
                 
                float deltaXeast = gisXeast - heightCellCentroidX;
                float deltaXwest = heightCellCentroidX - gisXwest;
                float deltaYnorth = gisYnorth - heightCellCentroidY;
                float deltaYsouth = heightCellCentroidY - gisYsouth;
                float demZ = gisInverseCellArea * (gisZsw * deltaXeast * deltaYnorth + gisZse * deltaXwest * deltaYnorth +
                                                   gisZnw * deltaXeast * deltaYsouth + gisZne * deltaXwest * deltaYsouth);
                this[cellIndex] = demZ;
            }
        }

        /// <summary>
        /// Calculate slope and aspect at a given point.
        /// </summary>
        /// <param name="point">metric coordinates of point to derive orientation</param>
        /// <param name="rslope_angle">slope angle as percentage (i.e: 1:=45 degrees)</param>
        /// <param name="rslope_aspect">slope direction in degrees (0: North, 90: east, 180: south, 270: west) <summary>
        /// calculate slope and aspect at a given point.</param>
        /// <returns>height at point (x/y)</returns>
        /// <remarks>
        /// Burrough PA, McDonell RA. 1998. Principles of Geographical Information Systems.(Oxford University Press, New York), p. 190.
        /// http://uqu.edu.sa/files2/tiny_mce/plugins/filemanager/files/4280125/Principles%20of%20Geographical%20Information%20Systems.pdf
        /// </remarks>
        // TODO: should probably not default to returning z = 0, slope = 0, aspect = 0
        public float GetSlopeAndAspect(PointF point, out float rslope_angle, out float rslope_aspect)
        {
            Point pointIndexXY = this.GetCellXYIndex(point);
            int cellIndexX = pointIndexXY.X;
            int cellIndexY = pointIndexXY.Y;
            if (cellIndexX > 0 && cellIndexX < this.CellsX + 1 && cellIndexY > 0 && cellIndexY < this.CellsY - 1)
            {
                int cellIndex = this.IndexXYToIndex(cellIndexX, cellIndexY);
                float p = this[cellIndexX, cellIndexY];
                float z2 = this[cellIndex - this.CellsX];
                float z4 = this[cellIndex - 1];
                float z6 = this[cellIndex + 1];
                float z8 = this[cellIndex + this.CellsX];
                float g = (-z4 + z6) / (2.0F * this.CellSizeInM);
                float h = (z2 - z8) / (2.0F * this.CellSizeInM);

                if (Single.IsNaN(z2) || Single.IsNaN(z4) || Single.IsNaN(z6) || Single.IsNaN(z8))
                {
                    rslope_angle = 0.0F;
                    rslope_aspect = 0.0F;
                    return p;
                }

                rslope_angle = MathF.Sqrt(g * g + h * h);
                // atan2: returns -pi : +pi
                // North: -pi/2, east: 0, south: +pi/2, west: -pi/+pi
                float aspect = MathF.Atan2(-h, -g);
                // transform to degree:
                // north: 0, east: 90, south: 180, west: 270
                aspect = aspect * 180.0F / Single.Pi + 360.0F + 90.0F;
                aspect %= 360.0F;

                rslope_aspect = aspect;
                return p;
            }
            else
            {
                rslope_angle = 0.0F;
                rslope_aspect = 0.0F;
                return 0.0F;
            }
        }

        public float GetTopographicPositionIndex(PointF point, float radius)
        {
            int rpix = (int)(radius / Constant.Grid.HeightCellSizeInM); // TODO: should this round?
            Point indexXY = this.GetCellXYIndex(point);
            int indexX = indexXY.X;
            int indexY = indexXY.Y;
            float point_elevation = this[indexX, indexY];
            int n = 0;
            float avg_elevation = 0.0F;
            for (int iy = Int32.Max(0, indexY - rpix); iy < Int32.Min(this.CellsY, indexY + rpix); ++iy)
            {
                for (int ix = Int32.Max(0, indexX - rpix); ix < Int32.Min(this.CellsX, indexX + rpix); ++ix)
                {
                    int dist = (ix - indexX) * (ix - indexX) + (iy - indexY) * (iy - indexY);
                    if (dist <= rpix * rpix)
                    {
                        avg_elevation += this[ix, iy];
                        ++n;
                    }
                }
            }
            if (n > 0)
            {
                return point_elevation - (avg_elevation / n);
            }
            return 0.0F;
        }

        public void CalculateSlopeAndAspectGridsIfNeeded()
        {
            if (this.slope_grid.IsSetup())
            {
                // setup custom grids with the same size as this DEM
                this.slope_grid.Setup(this);
                this.view_grid.Setup(this);
                this.AspectGrid.Setup(this);
            }
            else
            {
                return;
            }

            // use fixed values for azimuth (315) and angle (45 deg) and calculate
            // norm vectors
            float sun_x = MathF.Cos(315.0F * Single.Pi / 180.0F) * MathF.Cos(45.0F * Single.Pi / 180.0F);
            float sun_y = MathF.Sin(315.0F * Single.Pi / 180.0F) * MathF.Cos(45.0F * Single.Pi / 180.0F);
            float sun_z = MathF.Sin(45.0F * Single.Pi / 180.0F);

            float a_x, a_y, a_z;
            for (int cellIndex = 0; cellIndex < this.CellCount; ++cellIndex)
            {
                PointF pt = this.GetCellProjectCentroid(cellIndex);
                float height = GetSlopeAndAspect(pt, out float slope, out float aspect);
                this.slope_grid[cellIndex] = slope;
                this.AspectGrid[cellIndex] = aspect;

                // calculate the view value:
                if (height > 0)
                {
                    float h = MathF.Atan(slope);
                    a_x = MathF.Cos(aspect * Single.Pi / 180.0F) * MathF.Cos(h);
                    a_y = MathF.Sin(aspect * Single.Pi / 180.0F) * MathF.Cos(h);
                    a_z = MathF.Sin(h);

                    // use the scalar product to calculate the angle, and then
                    // transform from [-1,1] to [0,1]
                    this.view_grid[cellIndex] = (a_x * sun_x + a_y * sun_y + a_z * sun_z + 1.0F) / 2.0F;
                }
                else
                {
                    this.view_grid[cellIndex] = 0.0F;
                }
            }
        }
    }
}
