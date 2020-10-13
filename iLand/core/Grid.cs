using iLand.Tools;
using System;
using System.Drawing;
using System.Text;

namespace iLand.Core
{
    internal class Grid
    {
        public static bool LoadGridFromImage(string fileName, Grid<float> rGrid)
        {
            throw new NotImplementedException();
        }

        public static string ToEsriRaster<T>(Grid<T> grid, Func<T, string> valueFunction)
        {
            Vector3D model = new Vector3D(grid.PhysicalExtent.Left, grid.PhysicalExtent.Top, 0.0);
            Vector3D world = new Vector3D();
            GisGrid.ModelToWorld(model, world);
            string result = String.Format("ncols {0}{6}nrows {1}{6}xllcorner {2}{6}yllcorner {3}{6}cellsize {4}{6}NODATA_value {5}{6}",
                                           grid.CellsX, grid.CellsY, world.X, world.Y, grid.CellSize, -9999, System.Environment.NewLine);
            string line = Grid.ToString(grid, valueFunction, ' '); // for special grids
            return result + line;
        }

        public static string ToEsriRaster<T>(Grid<T> grid)
        {
            Vector3D model = new Vector3D(grid.PhysicalExtent.Left, grid.PhysicalExtent.Top, 0.0);
            Vector3D world = new Vector3D();
            GisGrid.ModelToWorld(model, world);
            StringBuilder result = new StringBuilder();
            result.Append(String.Format("ncols {0}{6}nrows {1}{6}xllcorner {2}{6}yllcorner {3}{6}cellsize {4}{6}NODATA_value {5}{6}",
                                        grid.CellsX, grid.CellsY, world.X, world.Y, grid.CellSize, -9999, System.Environment.NewLine));
            for (int y = grid.CellsY - 1; y >= 0; --y)
            {
                for (int x = 0; x < grid.CellsX; x++)
                {
                    result.Append(grid[x, y].ToString() + ' ');
                }
                result.Append(System.Environment.NewLine);
            }
            return result.ToString();
        }

        /// template version for non-float grids (see also version for Grid<float>)
        /// @param valueFunction pointer to a function with the signature: string func(T&) : this should return a string
        /// @param sep string separator
        /// @param newline_after if <>-1 a newline is added after every 'newline_after' data values
        public static string ToString<T>(Grid<T> grid, Func<T, string> valueFunction, char sep = ';', int newline_after = -1)
        {
            StringBuilder ts = new StringBuilder();

            int newl_counter = newline_after;
            for (int y = grid.CellsY - 1; y >= 0; --y)
            {
                for (int x = 0; x < grid.CellsX; x++)
                {
                    ts.Append(valueFunction(grid[x, y]) + sep);

                    if (--newl_counter == 0)
                    {
                        ts.Append(System.Environment.NewLine);
                        newl_counter = newline_after;
                    }
                }
                ts.Append(System.Environment.NewLine);
            }

            return ts.ToString();
        }
    }

    /** Grid class (template).
        @ingroup tools
        Orientation
        The grid is oriented as typically coordinates on the northern hemisphere: higher y-values -> north, higher x-values-> east.
        The projection is reversed for drawing on screen (Viewport).
                  N
          (0/2) (1/2) (2/2)
        W (0/1) (1/1) (2/1)  E
          (0/0) (1/0) (2/0)
                  S
        */
    public class Grid<T>
    {
        private T[] mData;

        /// get the length of one pixel of the grid
        public float CellSize { get; private set; }
        ///< returns the number of elements of the grid
        public int Count { get; private set; } 
        /// get the metric rectangle of the grid
        public RectangleF PhysicalExtent { get; private set; }
        public int CellsX { get; private set; }
        public int CellsY { get; private set; }

        public Grid()
        {
            this.mData = null;
            this.CellSize = 0.0F;
            this.CellsX = 0;
            this.CellsY = 0;
            this.Count = 0;
            this.PhysicalExtent = default;
        }

        public Grid(float cellsize, int sizex, int sizey)
        {
            mData = null;
            Setup(cellsize, sizex, sizey);
        }

        public Grid(RectangleF extent, float cellsize)
        {
            mData = null;
            Setup(extent, cellsize);
        }

        public Grid(Grid<T> toCopy)
        {
            mData = null;
            PhysicalExtent = toCopy.PhysicalExtent;
            Setup(toCopy.PhysicalExtent, toCopy.CellSize);
            //setup(toCopy.cellsize(), toCopy.sizeX(), toCopy.sizeY());
            Array.Copy(toCopy.mData, 0, this.mData, 0, toCopy.mData.Length);
        }

        /// use the square brackets to access by index
        public T this[int idx]
        {
            get { return mData[idx]; }
            set { mData[idx] = value; }
        }
        /// access (const) with index variables. use int.
        public T this[int ix, int iy]
        {
            get { return this.mData[iy * this.CellsX + ix]; }
            set { this.mData[iy * this.CellsX + ix] = value; }
        }
        /// access (const) using metric variables. use float.
        public T this[float x, float y]
        {
            get { return this[this.IndexAt(x, y)]; }
            set { this[this.IndexAt(x, y)] = value; }
        }
        /// access value of grid with a Point
        public T this[Point p]
        {
            get { return this[p.X, p.Y]; }
            set { this[p.X, p.Y] = value; }
        }

        /// use the square bracket to access by PointF
        public T this[PointF p]
        {
            get { return this[IndexAt(p)]; }
            set { this[IndexAt(p)] = value; }
        }

        public bool Contains(float x, float y)
        {
            return x >= PhysicalExtent.Left && x < PhysicalExtent.Right && y >= PhysicalExtent.Top && y < PhysicalExtent.Bottom;
        }

        ///< return true, if index is within the grid
        public bool Contains(int x, int y)
        {
            return (x >= 0 && x < CellsX && y >= 0 && y < CellsY);
        }

        public bool Contains(Point pos)
        {
            return this.Contains(pos.X, pos.Y);
        }

        public bool Contains(PointF pos)
        {
            return Contains(pos.X, pos.Y);
        }

        /// get the (metric) centerpoint of cell with index @p pos
        public PointF GetCellCenterPoint(Point pos) ///< get metric coordinates of the cells center
        {
            return new PointF((pos.X + 0.5F) * CellSize + PhysicalExtent.Left, (pos.Y + 0.5F) * CellSize + PhysicalExtent.Top);
        }

        /// get the metric cell center point of the cell given by index 'index'
        public PointF GetCellCenterPoint(int index)
        {
            Point pos = IndexOf(index);
            return new PointF((pos.X + 0.5F) * CellSize + PhysicalExtent.Left, (pos.Y + 0.5F) * CellSize + PhysicalExtent.Top);
        }

        /// get the metric rectangle of the cell with index @pos
        public RectangleF GetCellRect(Point pos) ///< return coordinates of rect given by @param pos.
        {
            RectangleF r = new RectangleF(PhysicalExtent.Left + CellSize * pos.X, PhysicalExtent.Top + pos.Y * CellSize, CellSize, CellSize);
            return r;
        }

        ///< get index of value at position pos (metric)
        public Point IndexAt(PointF pos)
        {
            return this.IndexAt(pos.X, pos.Y);
        }

        public Point IndexAt(float x, float y)
        {
            return new Point((int)((x - this.PhysicalExtent.Left) / this.CellSize), (int)((y - this.PhysicalExtent.Top) / this.CellSize));
        }

        /// get index (x/y) of the (linear) index 'index' (0..count-1)
        public Point IndexOf(int index)
        {
            return new Point(index % CellsX, index / CellsX);
        }

        ///< returns false if the grid was not setup
        public bool IsEmpty() { return mData == null; }

        /// returns the index of an aligned grid (with the same size and matching origin) with the double cell size (e.g. to scale from a 10m grid to a 20m grid)
        // public int index2(int idx) { return ((idx / mSizeX) / 2) * (mSizeX / 2) + (idx % mSizeX) / 2; }
        /// returns the index of an aligned grid (the same size) with the 5 times bigger cells (e.g. to scale from a 2m grid to a 10m grid)
        public int Index5(int idx) { return ((idx / CellsX) / 5) * (CellsX / 5) + (idx % CellsX) / 5; }
        /// returns the index of an aligned grid (the same size) with the 10 times bigger cells (e.g. to scale from a 2m grid to a 20m grid)
        public int Index10(int idx) { return ((idx / CellsX) / 10) * (CellsX / 10) + (idx % CellsX) / 10; }

        public int IndexOf(int ix, int iy) { return iy * CellsX + ix; } ///< get the 0-based index of the cell with indices ix and iy.
        public int IndexOf(Point pos) { return pos.Y * CellsX + pos.X; } ///< get the 0-based index of the cell at 'pos'.

        /// force @param pos to contain valid indices with respect to this grid.
        public void MakeValid(Point pos) ///< ensure that "pos" is a valid key. if out of range, pos is set to minimum/maximum values.
        {
            pos.X = Math.Max(Math.Min(pos.X, CellsX - 1), 0);
            pos.Y = Math.Max(Math.Min(pos.Y, CellsY - 1), 0);
        }

        /// get the rectangle of the grid in terms of indices
        public Rectangle CellExtent() { return new Rectangle(0, 0, CellsX, CellsY); }

        public void Clear()
        {
            // BUGBUG: what about all other fields?
            mData = null;
        }

        public void CopyFrom(Grid<T> source)
        {
            if (source.Count != this.Count)
            {
                throw new NotSupportedException();
            }
            Array.Copy(source.mData, 0, this.mData, 0, source.mData.Length);
        }

        public void Initialize(T value)
        {
            Array.Fill(this.mData, value);
        }

        public bool Setup(Grid<T> source)
        {
            this.Clear();
            return this.Setup(source.PhysicalExtent, source.CellSize);
        }

        public bool Setup(float cellSize, int cellsX, int cellsY)
        {
            this.CellsX = cellsX;
            this.CellsY = cellsY;
            this.PhysicalExtent = new RectangleF(this.PhysicalExtent.X, this.PhysicalExtent.Y, cellSize * cellsX, cellSize * cellsY);
            if (this.mData != null)
            {
                // test if we can re-use the allocated memory.
                if (this.CellsX * this.CellsY > this.Count)
                {
                    // we cannot re-use the memory - create new data
                    this.mData = null;
                }
            }
            this.CellSize = cellSize;
            this.Count = this.CellsX * this.CellsY;
            if (this.Count == 0)
            {
                return false;
            }
            if (this.mData == null)
            {
                this.mData = new T[this.Count];
            }
            return true;
        }

        public bool Setup(RectangleF extent, double cellSize)
        {
            if (cellSize <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }
            if (extent.IsEmpty)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            this.PhysicalExtent = extent;
            int dx = (int)(extent.Width / cellSize);
            if (this.PhysicalExtent.Left + cellSize * dx < extent.Right)
            {
                dx++;
            }
            int dy = (int)(extent.Height / cellSize);
            if (this.PhysicalExtent.Top + cellSize * dy < extent.Bottom)
            {
                dy++;
            }
            return Setup((float)cellSize, dx, dy);
        }

        public void ClearDefault()
        {
            Clear(default);
        }

        private void Clear(T value)
        {
            Initialize(value);
        }

        public double GetCenterToCenterCellDistance(Point p1, Point p2)
        {
            PointF fp1 = GetCellCenterPoint(p1);
            PointF fp2 = GetCellCenterPoint(p2);
            double distance = MathF.Sqrt((fp1.X - fp2.X) * (fp1.X - fp2.X) + (fp1.Y - fp2.Y) * (fp1.Y - fp2.Y));
            return distance;
        }

        /// dumps a Grid<T> to a long data CSV.
        /// rows will be y-lines, columns x-values. (see grid.cpp)
        public string ToCsv()
        {
            StringBuilder res = new StringBuilder();
            res.AppendLine("x_m,y_m,value"); // wrong if value overrides ToString() and returns multiple values
            for (int xIndex = 0; xIndex < this.CellsX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < this.CellsY; ++yIndex)
                {
                    PointF cellCenter = this.GetCellCenterPoint(new Point(xIndex, yIndex));
                    res.AppendLine(cellCenter.X + "," + cellCenter.Y + "," + this[xIndex, yIndex].ToString());
                }
            }
            return res.ToString();
        }
    }
}
