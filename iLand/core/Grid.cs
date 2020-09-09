using iLand.tools;
using System;
using System.Drawing;
using System.Text;

namespace iLand.core
{
    internal class Grid
    {
        public static bool loadGridFromImage(string fileName, Grid<float> rGrid)
        {
            throw new NotImplementedException();
        }

        public static string gridToESRIRaster<T>(Grid<T> grid, Func<T, string> valueFunction)
        {
            Vector3D model = new Vector3D(grid.metricRect().Left, grid.metricRect().Top, 0.0);
            Vector3D world = new Vector3D();
            GisGrid.modelToWorld(model, world);
            string result = String.Format("ncols {0}{6}nrows {1}{6}xllcorner {2}{6}yllcorner {3}{6}cellsize {4}{6}NODATA_value {5}{6}",
                                           grid.sizeX(), grid.sizeY(), world.x(), world.y(), grid.cellsize(), -9999, System.Environment.NewLine);
            string line = Grid.gridToString(grid, valueFunction, ' '); // for special grids
            return result + line;
        }

        public static string gridToESRIRaster<T>(Grid<T> grid)
        {
            Vector3D model = new Vector3D(grid.metricRect().Left, grid.metricRect().Top, 0.0);
            Vector3D world = new Vector3D();
            GisGrid.modelToWorld(model, world);
            string result = String.Format("ncols {0}{6}nrows {1}{6}xllcorner {2}{6}yllcorner {3}{6}cellsize {4}{6}NODATA_value {5}{6}",
                    grid.sizeX(), grid.sizeY(), world.x(), world.y(), grid.cellsize(), -9999, System.Environment.NewLine);
            string line = Grid.gridToString(grid, ' '); // for normal grids (e.g. float)
            return result + line;
        }

        /// dumps a Grid<float> to a String.
        /// rows will be y-lines, columns x-values. (see grid.cpp)
        public static string gridToString<T>(Grid<T> grid, char sep = ';', int newline_after = -1)
        {
            StringBuilder res = new StringBuilder();
            int newl_counter = newline_after;
            for (int y = grid.sizeY() - 1; y >= 0; --y)
            {
                for (int x = 0; x < grid.sizeX(); x++)
                {
                    res.Append(grid.constValueAtIndex(new Point(x, y)).ToString() + sep);
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
        public static string gridToString<T>(Grid<T> grid, Func<T, string> valueFunction, char sep = ';', int newline_after = -1)
        {
            StringBuilder ts = new StringBuilder();

            int newl_counter = newline_after;
            for (int y = grid.sizeY() - 1; y >= 0; --y)
            {
                for (int x = 0; x < grid.sizeX(); x++)
                {
                    ts.Append(valueFunction(grid.constValueAtIndex(x, y)) + sep);

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
        private RectangleF mRect;
        private float mCellsize; ///< size of a cell in meter
        private int mSizeX; ///< count of cells in x-direction
        private int mSizeY; ///< count of cells in y-direction
        private int mCount; ///< total number of cells in the grid

        public Grid()
        {
            mData = null;
            mCellsize = 0.0F;
            mSizeX = 0;
            mSizeY = 0;
            mCount = 0;
        }

        public Grid(float cellsize, int sizex, int sizey)
        {
            mData = null;
            setup(cellsize, sizex, sizey);
        }

        /// create from a metric rect
        public Grid(RectangleF rect_metric, float cellsize)
        {
            mData = null;
            setup(rect_metric, cellsize);
        }

        public Grid(Grid<T> toCopy)
        {
            mData = null;
            mRect = toCopy.mRect;
            setup(toCopy.metricRect(), toCopy.cellsize());
            //setup(toCopy.cellsize(), toCopy.sizeX(), toCopy.sizeY());
            Array.Copy(toCopy.mData, 0, this.mData, 0, toCopy.mData.Length);
        }

        public int sizeX() { return mSizeX; }
        public int sizeY() { return mSizeY; }
        // get the size of the grid in metric coordinates (x and y direction)
        public float metricSizeX() { return mSizeX * mCellsize; }
        public float metricSizeY() { return mSizeY * mCellsize; }
        /// get the metric rectangle of the grid
        public RectangleF metricRect() { return mRect; }
        /// get the rectangle of the grid in terms of indices
        public Rectangle rectangle() { return new Rectangle(0, 0, sizeX(), sizeY()); }
        /// get the length of one pixel of the grid
        public float cellsize() { return mCellsize; }
        public int count() { return mCount; } ///< returns the number of elements of the grid
        public bool isEmpty() { return mData == null; } ///< returns false if the grid was not setup

        /// use the square brackets to access by index
        public T this[int idx]
        {
            get { return mData[idx]; }
            set { mData[idx] = value; }
        }
        /// access (const) with index variables. use int.
        public T this[int ix, int iy]
        {
            get { return constValueAtIndex(ix, iy); }
            set { this.mData[iy * this.mSizeX + ix] = value; }
        }
        /// access (const) using metric variables. use float.
        public T this[float x, float y]
        {
            get { return constValueAt(x, y); }
            set { this[this.indexAt(new PointF(x, y))] = value; }
        }
        /// access value of grid with a Point
        public T this[Point p]
        {
            get { return constValueAtIndex(p); }
            set { this[p.X, p.Y] = value; }
        }

        /// use the square bracket to access by PointF
        public T this[PointF p]
        {
            get { return valueAt(p); }
            set { this[indexAt(p)] = value; }
        }

        /// value at position defined by a (integer) Point
        public T constValueAtIndex(Point pos) { return constValueAtIndex(pos.X, pos.Y); }
        /// value at position defined by a pair of integer coordinates
        public T constValueAtIndex(int ix, int iy) { return mData[iy * mSizeX + ix]; }
        /// value at position defined by the index within the grid
        public T constValueAtIndex(int index) { return mData[index]; } ///< get a ref ot value at (one-dimensional) index 'index'.

        public bool coordValid(float x, float y) { return x >= mRect.Left && x < mRect.Right && y >= mRect.Top && y < mRect.Bottom; }
        public bool coordValid(PointF pos) { return coordValid(pos.X, pos.Y); }

        ///< get index of value at position pos (metric)
        public Point indexAt(PointF pos) 
        { 
            return new Point((int)((pos.X - mRect.Left) / mCellsize), (int)((pos.Y - mRect.Top) / mCellsize)); 
        }

        /// get index (x/y) of the (linear) index 'index' (0..count-1)
        public Point indexOf(int index) { return new Point(index % mSizeX, index / mSizeX); }
        public bool isIndexValid(Point pos) { return (pos.X >= 0 && pos.X < mSizeX && pos.Y >= 0 && pos.Y < mSizeY); } ///< return true, if position is within the grid
        public bool isIndexValid(int x, int y) { return (x >= 0 && x < mSizeX && y >= 0 && y < mSizeY); } ///< return true, if index is within the grid

        /// returns the index of an aligned grid (with the same size and matching origin) with the double cell size (e.g. to scale from a 10m grid to a 20m grid)
        public int index2(int idx) { return ((idx / mSizeX) / 2) * (mSizeX / 2) + (idx % mSizeX) / 2; }
        /// returns the index of an aligned grid (the same size) with the 5 times bigger cells (e.g. to scale from a 2m grid to a 10m grid)
        public int index5(int idx) { return ((idx / mSizeX) / 5) * (mSizeX / 5) + (idx % mSizeX) / 5; }
        /// returns the index of an aligned grid (the same size) with the 10 times bigger cells (e.g. to scale from a 2m grid to a 20m grid)
        public int index10(int idx) { return ((idx / mSizeX) / 10) * (mSizeX / 10) + (idx % mSizeX) / 10; }

        public int index(int ix, int iy) { return iy * mSizeX + ix; } ///< get the 0-based index of the cell with indices ix and iy.
        public int index(Point pos) { return pos.Y * mSizeX + pos.X; } ///< get the 0-based index of the cell at 'pos'.

        /// force @param pos to contain valid indices with respect to this grid.
        public void validate(Point pos) ///< ensure that "pos" is a valid key. if out of range, pos is set to minimum/maximum values.
        {
            pos.X = Math.Max(Math.Min(pos.X, mSizeX - 1), 0);
            pos.Y = Math.Max(Math.Min(pos.Y, mSizeY - 1), 0);
        }

        public T valueAtIndex(Point pos) { return valueAtIndex(pos.X, pos.Y); }  ///< value at position defined by a Point defining the two indices (x,y)
        public T valueAtIndex(int ix, int iy) { return mData[iy * mSizeX + ix]; } ///< value at position defined by indices (x,y)
        public T valueAtIndex(int index) { return mData[index]; } ///< get a ref ot value at (one-dimensional) index 'index'.

        /// get the (metric) centerpoint of cell with index @p pos
        public PointF cellCenterPoint(Point pos) ///< get metric coordinates of the cells center
        {
            return new PointF((pos.X + 0.5F) * mCellsize + mRect.Left, (pos.Y + 0.5F) * mCellsize + mRect.Top);
        }

        /// get the metric cell center point of the cell given by index 'index'
        public PointF cellCenterPoint(int index)
        {
            Point pos = indexOf(index);
            return new PointF((pos.X + 0.5F) * mCellsize + mRect.Left, (pos.Y + 0.5F) * mCellsize + mRect.Top);
        }

        /// get the metric rectangle of the cell with index @pos
        public RectangleF cellRect(Point pos) ///< return coordinates of rect given by @param pos.
        {
            RectangleF r = new RectangleF(new PointF(mRect.Left + mCellsize * pos.X, mRect.Top + pos.Y * mCellsize), new SizeF(mCellsize, mCellsize));
            return r;
        }

        public T ptr(int x, int y) { return (mData[y * mSizeX + x]); } ///< get a pointer to the element indexed by "x" and "y"

        public void clear()
        {
            mData = null;
        }

        public void copy(Grid<T> source)
        {
            if (source.count() != count())
            {
                throw new NotSupportedException();
            }
            Array.Copy(source.mData, 0, this.mData, 0, source.mData.Length);
        }

        public void initialize(T value)
        {
            Array.Fill(this.mData, value);
        }

        public bool setup(Grid<T> source)
        {
            clear();
            mRect = source.mRect;
            return setup(source.mRect, source.mCellsize);
        }

        public bool setup(float cellsize, int sizex, int sizey)
        {
            mSizeX = sizex;
            mSizeY = sizey;
            if (mRect != null) // only set rect if not set before (e.g. by call to setup(RectangleF, double))
            {
                mRect = new RectangleF(0.0F, 0.0F, cellsize * sizex, cellsize * sizey);
            }
            if (mData != null)
            {
                // test if we can re-use the allocated memory.
                if (mSizeX * mSizeY > mCount || mCellsize != cellsize)
                {
                    // we cannot re-use the memory - create new data
                    mData = null;
                }
            }
            mCellsize = cellsize;
            mCount = mSizeX * mSizeY;
            if (mCount == 0)
            {
                return false;
            }
            if (mData == null)
            {
                mData = new T[mCount];
            }
            return true;
        }

        // unused in C++
        //private Grid<T> normalized(T targetvalue)
        //{
        //    Grid<T> target = new Grid<T>(this);
        //    T total = sum();
        //    T multiplier;
        //    if (total != 0)
        //    {
        //        multiplier = targetvalue / total;
        //    }
        //    else
        //    {
        //        return target;
        //    }
        //    for (int xIndex = 0; xIndex < this.sizeX(); ++xIndex)
        //    {
        //        for (int yIndex = 0; yIndex < this.sizeY(); ++yIndex)
        //        {
        //            this[xIndex, yIndex] *= multiplier;
        //        }
        //    }
        //    return target;
        //}

        public T valueAt(float x, float y)
        {
            return valueAtIndex(indexAt(new PointF(x, y)));
        }

        public T constValueAt(float x, float y)
        {
            return constValueAtIndex(indexAt(new PointF(x, y)));
        }

        public T valueAt(PointF posf)
        {
            return valueAtIndex(indexAt(posf));
        }

        public T constValueAt(PointF posf)
        {
            return constValueAtIndex(indexAt(posf));
        }

        public bool setup(RectangleF rect, double cellsize)
        {
            mRect = rect;
            int dx = (int)(rect.Width / cellsize);
            if (mRect.Left + cellsize * dx < rect.Right)
            {
                dx++;
            }
            int dy = (int)(rect.Height / cellsize);
            if (mRect.Top + cellsize * dy < rect.Bottom)
            {
                dy++;
            }
            return setup((float)cellsize, dx, dy);
        }

        // unused in C++
        //private T avg()
        //{
        //    if (count() != 0)
        //    {
        //        return sum() / count();
        //    }
        //    else
        //    {
        //        return 0;
        //    }
        //}

        // unused in C++
        //private void add(T summand)
        //{
        //    for (int xIndex = 0; xIndex < this.sizeX(); ++xIndex)
        //    {
        //        for (int yIndex = 0; yIndex < this.sizeY(); ++yIndex)
        //        {
        //            this[xIndex, yIndex] += summand;
        //        }
        //    }
        //}

        public void wipe()
        {
            wipe(default);
        }

        private void wipe(T value)
        {
            initialize(value);
        }

        public double distance(Point p1, Point p2)
        {
            PointF fp1 = cellCenterPoint(p1);
            PointF fp2 = cellCenterPoint(p2);
            double distance = MathF.Sqrt((fp1.X - fp2.X) * (fp1.X - fp2.X) + (fp1.Y - fp2.Y) * (fp1.Y - fp2.Y));
            return distance;
        }

        private Point randomPosition()
        {
            return new Point(RandomGenerator.irandom(0, mSizeX), RandomGenerator.irandom(0, mSizeY));
        }
    }
}
