using iLand.tools;
using System;
using System.Drawing;
using System.Text;

namespace iLand.core
{
    internal class Grid
    {
        public static bool LoadGridFromImage(string fileName, Grid<float> rGrid)
        {
            throw new NotImplementedException();
        }

        public static string ToEsriRaster<T>(Grid<T> grid, Func<T, string> valueFunction)
        {
            Vector3D model = new Vector3D(grid.PhysicalSize.Left, grid.PhysicalSize.Top, 0.0);
            Vector3D world = new Vector3D();
            GisGrid.ModelToWorld(model, world);
            string result = String.Format("ncols {0}{6}nrows {1}{6}xllcorner {2}{6}yllcorner {3}{6}cellsize {4}{6}NODATA_value {5}{6}",
                                           grid.SizeX, grid.SizeY, world.X, world.Y, grid.CellSize, -9999, System.Environment.NewLine);
            string line = Grid.ToString(grid, valueFunction, ' '); // for special grids
            return result + line;
        }

        public static string ToEsriRaster<T>(Grid<T> grid)
        {
            Vector3D model = new Vector3D(grid.PhysicalSize.Left, grid.PhysicalSize.Top, 0.0);
            Vector3D world = new Vector3D();
            GisGrid.ModelToWorld(model, world);
            string result = String.Format("ncols {0}{6}nrows {1}{6}xllcorner {2}{6}yllcorner {3}{6}cellsize {4}{6}NODATA_value {5}{6}",
                    grid.SizeX, grid.SizeY, world.X, world.Y, grid.CellSize, -9999, System.Environment.NewLine);
            string line = Grid.ToString(grid, ' '); // for normal grids (e.g. float)
            return result + line;
        }

        /// dumps a Grid<float> to a String.
        /// rows will be y-lines, columns x-values. (see grid.cpp)
        public static string ToString<T>(Grid<T> grid, char sep = ';', int newline_after = -1)
        {
            StringBuilder res = new StringBuilder();
            int newl_counter = newline_after;
            for (int y = grid.SizeY - 1; y >= 0; --y)
            {
                for (int x = 0; x < grid.SizeX; x++)
                {
                    res.Append(grid[x, y].ToString() + sep);
                    if (--newl_counter == 0)
                    {
                        res.Append(System.Environment.NewLine);
                        newl_counter = newline_after;
                    }
                }
                res.Append(System.Environment.NewLine);
            }
            return res.ToString();
        }

        /// template version for non-float grids (see also version for Grid<float>)
        /// @param valueFunction pointer to a function with the signature: string func(T&) : this should return a string
        /// @param sep string separator
        /// @param newline_after if <>-1 a newline is added after every 'newline_after' data values
        public static string ToString<T>(Grid<T> grid, Func<T, string> valueFunction, char sep = ';', int newline_after = -1)
        {
            StringBuilder ts = new StringBuilder();

            int newl_counter = newline_after;
            for (int y = grid.SizeY - 1; y >= 0; --y)
            {
                for (int x = 0; x < grid.SizeX; x++)
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
    internal class Grid<T>
    {
        private T[] mData;

        /// get the length of one pixel of the grid
        public float CellSize { get; private set; }
        ///< returns the number of elements of the grid
        public int Count { get; private set; } 
        /// get the metric rectangle of the grid
        public RectangleF PhysicalSize { get; private set; }
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }

        public Grid()
        {
            mData = null;
            CellSize = 0.0F;
            SizeX = 0;
            SizeY = 0;
            Count = 0;
        }

        public Grid(float cellsize, int sizex, int sizey)
        {
            mData = null;
            Setup(cellsize, sizex, sizey);
        }

        /// create from a metric rect
        public Grid(RectangleF rect_metric, float cellsize)
        {
            mData = null;
            Setup(rect_metric, cellsize);
        }

        public Grid(Grid<T> toCopy)
        {
            mData = null;
            PhysicalSize = toCopy.PhysicalSize;
            Setup(toCopy.PhysicalSize, toCopy.CellSize);
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
            get { return this.mData[iy * this.SizeX + ix]; }
            set { this.mData[iy * this.SizeX + ix] = value; }
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
            return x >= PhysicalSize.Left && x < PhysicalSize.Right && y >= PhysicalSize.Top && y < PhysicalSize.Bottom;
        }

        ///< return true, if index is within the grid
        public bool Contains(int x, int y)
        {
            return (x >= 0 && x < SizeX && y >= 0 && y < SizeY);
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
            return new PointF((pos.X + 0.5F) * CellSize + PhysicalSize.Left, (pos.Y + 0.5F) * CellSize + PhysicalSize.Top);
        }

        /// get the metric cell center point of the cell given by index 'index'
        public PointF GetCellCenterPoint(int index)
        {
            Point pos = IndexOf(index);
            return new PointF((pos.X + 0.5F) * CellSize + PhysicalSize.Left, (pos.Y + 0.5F) * CellSize + PhysicalSize.Top);
        }

        /// get the metric rectangle of the cell with index @pos
        public RectangleF GetCellRect(Point pos) ///< return coordinates of rect given by @param pos.
        {
            RectangleF r = new RectangleF(new PointF(PhysicalSize.Left + CellSize * pos.X, PhysicalSize.Top + pos.Y * CellSize), new SizeF(CellSize, CellSize));
            return r;
        }

        ///< get index of value at position pos (metric)
        public Point IndexAt(PointF pos)
        {
            return this.IndexAt(pos.X, pos.Y);
        }

        public Point IndexAt(float x, float y)
        {
            return new Point((int)((x - this.PhysicalSize.Left) / this.CellSize), (int)((y - this.PhysicalSize.Top) / this.CellSize));
        }

        /// get index (x/y) of the (linear) index 'index' (0..count-1)
        public Point IndexOf(int index)
        {
            return new Point(index % SizeX, index / SizeX);
        }

        ///< returns false if the grid was not setup
        public bool IsEmpty() { return mData == null; }

        /// returns the index of an aligned grid (with the same size and matching origin) with the double cell size (e.g. to scale from a 10m grid to a 20m grid)
        // public int index2(int idx) { return ((idx / mSizeX) / 2) * (mSizeX / 2) + (idx % mSizeX) / 2; }
        /// returns the index of an aligned grid (the same size) with the 5 times bigger cells (e.g. to scale from a 2m grid to a 10m grid)
        public int Index5(int idx) { return ((idx / SizeX) / 5) * (SizeX / 5) + (idx % SizeX) / 5; }
        /// returns the index of an aligned grid (the same size) with the 10 times bigger cells (e.g. to scale from a 2m grid to a 20m grid)
        public int Index10(int idx) { return ((idx / SizeX) / 10) * (SizeX / 10) + (idx % SizeX) / 10; }

        public int IndexOf(int ix, int iy) { return iy * SizeX + ix; } ///< get the 0-based index of the cell with indices ix and iy.
        public int IndexOf(Point pos) { return pos.Y * SizeX + pos.X; } ///< get the 0-based index of the cell at 'pos'.

        /// force @param pos to contain valid indices with respect to this grid.
        public void MakeValid(Point pos) ///< ensure that "pos" is a valid key. if out of range, pos is set to minimum/maximum values.
        {
            pos.X = Math.Max(Math.Min(pos.X, SizeX - 1), 0);
            pos.Y = Math.Max(Math.Min(pos.Y, SizeY - 1), 0);
        }

        // get the size of the grid in metric coordinates (x and y direction)
        public float PhysicalSizeX() { return SizeX * CellSize; }
        public float PhysicalSizeY() { return SizeY * CellSize; }

        /// get the rectangle of the grid in terms of indices
        public Rectangle Size() { return new Rectangle(0, 0, SizeX, SizeY); }

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
            Clear();
            PhysicalSize = source.PhysicalSize; // BUGBUG: deep copy?
            return Setup(source.PhysicalSize, source.CellSize);
        }

        public bool Setup(float cellsize, int sizex, int sizey)
        {
            SizeX = sizex;
            SizeY = sizey;
            if (PhysicalSize != null) // only set rect if not set before (e.g. by call to setup(RectangleF, double))
            {
                PhysicalSize = new RectangleF(0.0F, 0.0F, cellsize * sizex, cellsize * sizey);
            }
            if (mData != null)
            {
                // test if we can re-use the allocated memory.
                if (SizeX * SizeY > Count || CellSize != cellsize)
                {
                    // we cannot re-use the memory - create new data
                    mData = null;
                }
            }
            CellSize = cellsize;
            Count = SizeX * SizeY;
            if (Count == 0)
            {
                return false;
            }
            if (mData == null)
            {
                mData = new T[Count];
            }
            return true;
        }

        public bool Setup(RectangleF rect, double cellsize)
        {
            PhysicalSize = rect;
            int dx = (int)(rect.Width / cellsize);
            if (PhysicalSize.Left + cellsize * dx < rect.Right)
            {
                dx++;
            }
            int dy = (int)(rect.Height / cellsize);
            if (PhysicalSize.Top + cellsize * dy < rect.Bottom)
            {
                dy++;
            }
            return Setup((float)cellsize, dx, dy);
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
    }
}
