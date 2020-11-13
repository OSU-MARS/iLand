using iLand.Tools;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace iLand.World
{
    internal class Grid
    {
        public static bool LoadGridFromImage(string fileName, Grid<float> rGrid)
        {
            throw new NotImplementedException();
        }

        //public static string ToEsriRaster<T>(Grid<T> grid, Func<T, string> valueFunction)
        //{
        //    Vector3D model = new Vector3D(grid.PhysicalExtent.Left, grid.PhysicalExtent.Top, 0.0);
        //    Vector3D world = new Vector3D();
        //    GisGrid.ModelToWorld(model, world);
        //    string result = String.Format("ncols {0}{6}nrows {1}{6}xllcorner {2}{6}yllcorner {3}{6}cellsize {4}{6}NODATA_value {5}{6}",
        //                                   grid.CellsX, grid.CellsY, world.X, world.Y, grid.CellSize, -9999, System.Environment.NewLine);
        //    string line = Grid.ToString(grid, valueFunction, ' '); // for special grids
        //    return result + line;
        //}

        public static string ToEsriRaster<T>(Landscape landscape, Grid<T> grid) where T : notnull
        {
            Vector3D local = new Vector3D(grid.PhysicalExtent.Left, grid.PhysicalExtent.Top, 0.0F);
            landscape.Environment.GisGrid.ModelToWorld(local, out Vector3D world);
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
        public static string ToString<T>(Grid<T> grid, Func<T, string> valueFunction, char sep = ';', int newlineAfter = -1)
        {
            StringBuilder stringBuilder = new StringBuilder();

            int newlineCounter = newlineAfter;
            for (int y = grid.CellsY - 1; y >= 0; --y)
            {
                for (int x = 0; x < grid.CellsX; x++)
                {
                    stringBuilder.Append(valueFunction(grid[x, y]) + sep);

                    if (--newlineCounter == 0)
                    {
                        stringBuilder.Append(System.Environment.NewLine);
                        newlineCounter = newlineAfter;
                    }
                }
                stringBuilder.Append(System.Environment.NewLine);
            }

            return stringBuilder.ToString();
        }
    }

    /** Grid class (template).
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
        private T[]? data;

        /// get the length of one pixel of the grid
        public float CellSize { get; private set; }
        // returns the number of elements of the grid
        public int Count { get; private set; } 
        /// get the metric rectangle of the grid
        public RectangleF PhysicalExtent { get; private set; }
        public int CellsX { get; private set; }
        public int CellsY { get; private set; }

        public Grid()
        {
            this.data = null;
            this.CellSize = 0.0F;
            this.CellsX = 0;
            this.CellsY = 0;
            this.Count = 0;
            this.PhysicalExtent = default;
        }

        public Grid(int sizeX, int sizeY, float cellSize)
        {
            this.data = null;
            this.Setup(sizeX, sizeY, cellSize);
        }

        public Grid(RectangleF extent, float cellSize)
        {
            this.Setup(extent, cellSize);
        }

        public Grid(Grid<T> other)
        {
            this.Setup(other.PhysicalExtent, other.CellSize);
            //setup(toCopy.cellsize(), toCopy.sizeX(), toCopy.sizeY());
            Array.Copy(other.data!, 0, this.data!, 0, other.data!.Length);
        }

        /// use the square brackets to access by index
        public T this[int index]
        {
            get { return this.data![index]; }
            set { this.data![index] = value; }
        }
        /// access (const) with index variables. use int.
        public T this[int indexX, int indexY]
        {
            get { return this.data![indexY * this.CellsX + indexX]; }
            set { this.data![indexY * this.CellsX + indexX] = value; }
        }

        public T this[int indexX, int indexY, int divisor]
        {
            get 
            {
                Debug.Assert(indexX >= 0 && indexY >= 0 && divisor > 0);
                return this[indexX / divisor, indexY / divisor];
            }
            set
            {
                Debug.Assert(indexX >= 0 && indexY >= 0 && divisor > 0);
                this[indexX / divisor, indexY / divisor] = value;
            }
        }

        /// access (const) using metric variables. use float.
        public T this[float x, float y]
        {
            get { return this[this.GetCellIndex(x, y)]; }
            set { this[this.GetCellIndex(x, y)] = value; }
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
            get { return this[GetCellIndex(p)]; }
            set { this[GetCellIndex(p)] = value; }
        }

        public bool Contains(float x, float y)
        {
            return x >= this.PhysicalExtent.Left && x < this.PhysicalExtent.Right && y >= this.PhysicalExtent.Top && y < this.PhysicalExtent.Bottom;
        }

        // return true, if index is within the grid
        public bool Contains(int x, int y)
        {
            return (x >= 0 && x < this.CellsX && y >= 0 && y < this.CellsY);
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
        public PointF GetCellCenterPosition(Point cell) // get metric coordinates of the cells center
        {
            return new PointF((cell.X + 0.5F) * CellSize + PhysicalExtent.Left, (cell.Y + 0.5F) * CellSize + PhysicalExtent.Top);
        }

        /// get the metric cell center point of the cell given by index 'index'
        public PointF GetCellCenterPosition(int index)
        {
            return this.GetCellCenterPosition(this.GetCellPosition(index));
        }

        /// get the metric rectangle of the cell with index @pos
        public RectangleF GetCellExtent(Point cell) // return coordinates of rect given by @param pos.
        {
            RectangleF extent = new RectangleF(this.PhysicalExtent.Left + this.CellSize * cell.X, this.PhysicalExtent.Top + cell.Y * this.CellSize, this.CellSize, this.CellSize);
            return extent;
        }

        // get index of value at position pos (metric)
        public Point GetCellIndex(PointF pos)
        {
            return this.GetCellIndex(pos.X, pos.Y);
        }

        public Point GetCellIndex(float x, float y)
        {
            // C++ version incorrectly assumes integer trunction rounds towards minus infinity rather than towards zero
            int xIndex = (int)MathF.Floor((x - this.PhysicalExtent.Left) / this.CellSize);
            int yIndex = (int)MathF.Floor((y - this.PhysicalExtent.Top) / this.CellSize);
            return new Point(xIndex, yIndex);
        }

        /// get index (x/y) of the (linear) index 'index' (0..count-1)
        public Point GetCellPosition(int index)
        {
            return new Point(index % CellsX, index / CellsX);
        }

        public bool IsNotSetup() { return this.data == null; }
        
        /// returns the index of an aligned grid (with the same size and matching origin) with the doubled cell size (e.g. to scale from a 10m grid to a 20m grid)
        // public int index2(int idx) { return ((idx / mSizeX) / 2) * (mSizeX / 2) + (idx % mSizeX) / 2; }
        /// returns the index of an aligned grid (the same size) with the 5 times bigger cells (e.g. to scale from a 2m grid to a 10m grid)
        public int Index5(int index) { return ((index / this.CellsX) / 5) * (this.CellsX / 5) + (index % this.CellsX) / 5; }
        /// returns the index of an aligned grid (the same size) with the 10 times bigger cells (e.g. to scale from a 2m grid to a 20m grid)
        public int Index10(int index) { return ((index / this.CellsX) / 10) * (this.CellsX / 10) + (index % this.CellsX) / 10; }

        public int IndexOf(int indexX, int indexY) { return indexY * this.CellsX + indexX; } // get the 0-based index of the cell with indices ix and iy.
        public int IndexOf(Point cell) { return cell.Y * this.CellsX + cell.X; } // get the 0-based index of the cell at 'pos'.

        /// force @param pos to contain valid indices with respect to this grid.
        public void Limit(Point cell) // ensure that "pos" is a valid key. if out of range, pos is set to minimum/maximum values.
        {
            cell.X = Math.Max(Math.Min(cell.X, this.CellsX - 1), 0);
            cell.Y = Math.Max(Math.Min(cell.Y, this.CellsY - 1), 0);
        }

        /// get the rectangle of the grid in terms of indices
        public Rectangle GetCellExtent() { return new Rectangle(0, 0, this.CellsX, this.CellsY); }

        public void Clear()
        {
            // BUGBUG: what about all other fields?
            data = null;
        }

        public void CopyFrom(Grid<T> source)
        {
            if ((this.IsNotSetup() == false) || (source.IsNotSetup() == false))
            {
                throw new NotSupportedException("Either target or destination grid is not setup.");
            }
            if ((this.CellSize != source.CellSize) || (this.CellsX != source.CellsX) || (this.CellsY != source.CellsY) || (source.Count != this.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }
            this.PhysicalExtent = source.PhysicalExtent;
            Array.Copy(source.data!, 0, this.data!, 0, source.data!.Length);
        }

        public void Fill(T? value)
        {
            if (this.IsNotSetup())
            {
                throw new NotSupportedException();
            }
            Array.Fill(this.data!, value);
        }

        public bool Setup(Grid<T> source)
        {
            this.Clear();
            return this.Setup(source.PhysicalExtent, source.CellSize);
        }

        public bool Setup(int cellsX, int cellsY, float cellSize)
        {
            this.CellsX = cellsX;
            this.CellsY = cellsY;
            this.PhysicalExtent = new RectangleF(this.PhysicalExtent.X, this.PhysicalExtent.Y, cellSize * cellsX, cellSize * cellsY);
            if (this.data != null)
            {
                // test if we can re-use the allocated memory.
                if (this.CellsX * this.CellsY > this.Count)
                {
                    // we cannot re-use the memory - create new data
                    this.data = null;
                }
            }
            this.CellSize = cellSize;
            this.Count = this.CellsX * this.CellsY;
            if (this.Count == 0)
            {
                return false;
            }
            if (this.data == null)
            {
                this.data = new T[this.Count];
            }
            return true;
        }

        public bool Setup(RectangleF extent, float cellSize)
        {
            if (cellSize <= 0.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }
            if (extent.IsEmpty)
            {
                throw new ArgumentOutOfRangeException(nameof(extent));
            }

            this.PhysicalExtent = extent;
            int cellsX = (int)(extent.Width / cellSize);
            if (this.PhysicalExtent.Left + cellSize * cellsX < extent.Right)
            {
                ++cellsX;
            }
            int cellsY = (int)(extent.Height / cellSize);
            if (this.PhysicalExtent.Top + cellSize * cellsY < extent.Bottom)
            {
                ++cellsY;
            }
            return this.Setup(cellsX, cellsY, cellSize);
        }

        public void FillDefault()
        {
            this.Fill(default);
        }

        public float GetCenterToCenterCellDistance(Point p1, Point p2)
        {
            PointF fp1 = GetCellCenterPosition(p1);
            PointF fp2 = GetCellCenterPosition(p2);
            float distance = MathF.Sqrt((fp1.X - fp2.X) * (fp1.X - fp2.X) + (fp1.Y - fp2.Y) * (fp1.Y - fp2.Y));
            return distance;
        }

        /// dumps a Grid<T> to a long data CSV.
        /// rows will be y-lines, columns x-values. (see grid.cpp)
        public string ToCsv()
        {
            StringBuilder csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("x_m,y_m,value"); // wrong if value overrides ToString() and returns multiple values but OK for now
            for (int xIndex = 0; xIndex < this.CellsX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < this.CellsY; ++yIndex)
                {
                    PointF cellCenter = this.GetCellCenterPosition(new Point(xIndex, yIndex));
                    csvBuilder.AppendLine(cellCenter.X + "," + cellCenter.Y + "," + this[xIndex, yIndex]!.ToString());
                }
            }
            return csvBuilder.ToString();
        }
    }
}
