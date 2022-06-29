using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text;

namespace iLand.World
{
    /// <remarks>
    /// The grid is oriented as is typical northern hemisphere projected coordinate systems: higher y values are farther north, 
    /// higher x values are farther east.
    ///                N
    ///       (0, 2) (1, 2) (2, 2)
    ///     W (0, 1) (1, 1) (2, 1)  E
    ///       (0, 0) (1, 0) (2, 0)
    ///                S
    /// </remarks>
    public class Grid<T>
    {
        private T[]? data;

        /// get the length of one pixel of the grid
        public float CellSize { get; private set; }
        // returns the number of elements of the grid
        public int Count { get; private set; } 
        /// get the metric rectangle of the grid
        public RectangleF PhysicalExtent { get; private set; }
        public int SizeX { get; private set; }
        public int SizeY { get; private set; }

        public Grid()
        {
            this.data = null;
            this.CellSize = 0.0F;
            this.SizeX = 0;
            this.SizeY = 0;
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
            if (other.data == null)
            {
                throw new ArgumentOutOfRangeException(nameof(other));
            }

            this.Setup(other.PhysicalExtent, other.CellSize);
            Array.Copy(other.data, 0, this.data, 0, other.data.Length);
        }


        public T this[int index]
        {
            get { return this.data![index]; }
            set { this.data![index] = value; }
        }

        public T this[int indexX, int indexY]
        {
            get { return this.data![indexY * this.SizeX + indexX]; }
            set { this.data![indexY * this.SizeX + indexX] = value; }
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

        public T this[float x, float y]
        {
            get { return this[this.GetCellXYIndex(x, y)]; }
            set { this[this.GetCellXYIndex(x, y)] = value; }
        }

        public T this[Point p]
        {
            get { return this[p.X, p.Y]; }
            set { this[p.X, p.Y] = value; }
        }

        /// use the square bracket to access by PointF
        public T this[PointF p]
        {
            get { return this[GetCellXYIndex(p)]; }
            set { this[GetCellXYIndex(p)] = value; }
        }

        public bool Contains(float x, float y)
        {
            return x >= this.PhysicalExtent.Left && x < this.PhysicalExtent.Right && y >= this.PhysicalExtent.Top && y < this.PhysicalExtent.Bottom;
        }

        // return true, if index is within the grid
        public bool Contains(int x, int y)
        {
            return (x >= 0 && x < this.SizeX && y >= 0 && y < this.SizeY);
        }

        public bool Contains(Point pos)
        {
            return this.Contains(pos.X, pos.Y);
        }

        public bool Contains(PointF pos)
        {
            return Contains(pos.X, pos.Y);
        }

        public PointF GetCellCentroid(Point cellIndex) // get metric coordinates of the cells center
        {
            return new PointF((cellIndex.X + 0.5F) * this.CellSize + this.PhysicalExtent.Left, (cellIndex.Y + 0.5F) * this.CellSize + this.PhysicalExtent.Top);
        }

        /// get the metric cell center point of the cell given by index 'index'
        public PointF GetCellCentroid(int cellIndex)
        {
            return this.GetCellCentroid(this.GetCellXYIndex(cellIndex));
        }

        /// get the metric rectangle of the cell with index @pos
        public RectangleF GetCellExtent(Point cell) // return coordinates of rect given by @param pos.
        {
            RectangleF extent = new(this.PhysicalExtent.Left + this.CellSize * cell.X, this.PhysicalExtent.Top + cell.Y * this.CellSize, this.CellSize, this.CellSize);
            return extent;
        }

        // get index of value at position pos (metric)
        public Point GetCellXYIndex(PointF pos)
        {
            return this.GetCellXYIndex(pos.X, pos.Y);
        }

        public Point GetCellXYIndex(float x, float y)
        {
            // C++ version incorrectly assumes integer trunction rounds towards minus infinity rather than towards zero
            int xIndex = (int)MathF.Floor((x - this.PhysicalExtent.Left) / this.CellSize);
            int yIndex = (int)MathF.Floor((y - this.PhysicalExtent.Top) / this.CellSize);
            return new Point(xIndex, yIndex);
        }

        /// get index (x/y) of the (linear) index 'index' (0..count-1)
        public Point GetCellXYIndex(int index)
        {
            return new Point(index % this.SizeX, index / this.SizeX);
        }

        [MemberNotNullWhen(true, nameof(Grid<T>.data))]
        public bool IsSetup()
        { 
            return this.data != null; 
        }
        
        /// returns the index of an aligned grid (with the same size and matching origin) with the doubled cell size (e.g. to scale from a 10m grid to a 20m grid)
        // public int index2(int idx) { return ((idx / mSizeX) / 2) * (mSizeX / 2) + (idx % mSizeX) / 2; }
        /// returns the index of an aligned grid (the same size) with the 5 times bigger cells (e.g. to scale from a 2m grid to a 10m grid)
        public int Index5(int index) { return ((index / this.SizeX) / 5) * (this.SizeX / 5) + (index % this.SizeX) / 5; }
        /// returns the index of an aligned grid (the same size) with the 10 times bigger cells (e.g. to scale from a 2m grid to a 20m grid)
        public int Index10(int index) { return ((index / this.SizeX) / 10) * (this.SizeX / 10) + (index % this.SizeX) / 10; }

        public int IndexOf(int indexX, int indexY) { return indexY * this.SizeX + indexX; } // get the 0-based index of the cell with indices ix and iy.
        public int IndexOf(Point cell) { return cell.Y * this.SizeX + cell.X; } // get the 0-based index of the cell at 'pos'.

        /// force @param pos to contain valid indices with respect to this grid.
        public void Limit(Point cell) // ensure that "pos" is a valid key. if out of range, pos is set to minimum/maximum values.
        {
            cell.X = Math.Max(Math.Min(cell.X, this.SizeX - 1), 0);
            cell.Y = Math.Max(Math.Min(cell.Y, this.SizeY - 1), 0);
        }

        public void Clear()
        {
            // BUGBUG: what about all other fields?
            this.data = null;
        }

        public void CopyFrom(Grid<T> source)
        {
            if ((this.IsSetup() == false) || (source.IsSetup() == false))
            {
                throw new NotSupportedException("Either target or destination grid is not setup.");
            }
            if ((this.CellSize != source.CellSize) || (this.SizeX != source.SizeX) || (this.SizeY != source.SizeY) || (source.Count != this.Count))
            {
                throw new ArgumentOutOfRangeException(nameof(source));
            }
            this.PhysicalExtent = source.PhysicalExtent;
            Array.Copy(source.data!, 0, this.data!, 0, source.data!.Length);
        }

        public void Fill(T value)
        {
            if (this.IsSetup() == false)
            {
                throw new NotSupportedException();
            }
            Array.Fill(this.data, value);
        }

        public float GetCenterToCenterCellDistance(Point p1, Point p2)
        {
            PointF fp1 = GetCellCentroid(p1);
            PointF fp2 = GetCellCentroid(p2);
            float distance = MathF.Sqrt((fp1.X - fp2.X) * (fp1.X - fp2.X) + (fp1.Y - fp2.Y) * (fp1.Y - fp2.Y));
            return distance;
        }

        [MemberNotNull(nameof(Grid<T>.data))]
        public bool Setup(Grid<T> source)
        {
            this.Clear();
            return this.Setup(source.PhysicalExtent, source.CellSize);
        }

        [MemberNotNull(nameof(Grid<T>.data))]
        public bool Setup(RectangleF extent, float cellSize)
        {
            if (cellSize <= 0.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }
            if ((extent.Width <= 0.0F) || (extent.Height <= 0.0F))
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

        [MemberNotNull(nameof(Grid<T>.data))]
        public bool Setup(int cellsX, int cellsY, float cellSize)
        {
            if (cellsX < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsX));
            }
            if (cellsY < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsY));
            }
            if (cellSize < 0.0F)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }

            if (this.data != null)
            {
                // reuse the data array that's already been allocated if it's large enough
                // If needed, shrinkage of the array can be supported.
                if (cellsX * cellsY > this.Count)
                {
                    this.data = null;
                }
            }

            this.CellSize = cellSize;
            this.Count = cellsX * cellsY;
            this.SizeX = cellsX;
            this.SizeY = cellsY;
            this.PhysicalExtent = new RectangleF(this.PhysicalExtent.X, this.PhysicalExtent.Y, cellSize * cellsX, cellSize * cellsY);

            if (this.data == null)
            {
                this.data = new T[this.Count];
            }
            return true;
        }

        /// dumps a Grid<T> to a long data CSV.
        /// rows will be y-lines, columns x-values. (see grid.cpp)
        public string ToCsv()
        {
            StringBuilder csvBuilder = new();
            csvBuilder.AppendLine("x_m,y_m,value"); // wrong if value overrides ToString() and returns multiple values but OK for now
            for (int xIndex = 0; xIndex < this.SizeX; ++xIndex)
            {
                for (int yIndex = 0; yIndex < this.SizeY; ++yIndex)
                {
                    PointF cellCenter = this.GetCellCentroid(new Point(xIndex, yIndex));
                    csvBuilder.AppendLine(cellCenter.X + "," + cellCenter.Y + "," + this[xIndex, yIndex]!.ToString());
                }
            }
            return csvBuilder.ToString();
        }
    }
}
