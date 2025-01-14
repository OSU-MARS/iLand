﻿// C++/tools/{ grid.h, grid.cpp }
using System;
using System.Diagnostics;
using System.Drawing;

namespace iLand.World
{
    /// <remarks>
    /// The grid is oriented as is typical northern hemisphere projected coordinate systems: higher y values are farther north, 
    /// higher x values are farther east.
    /// 
    ///                N                        N
    ///       (0, 2) (1, 2) (2, 2)         (6) (7) (8)
    ///     W (0, 1) (1, 1) (2, 1)  E    W (3) (4) (5) E
    ///       (0, 0) (1, 0) (2, 0)         (0) (1) (2)
    ///                S                        S
    /// 
    /// The grid can be accessed with both (x, y) indexing and with a single index. Single indexing starts with the (0, 0) cell
    /// and proceeds west to east and south to north following the coordinate system (single = y * SizeX + x)
    /// </remarks>
    public class Grid<T>
    {
        /// get the length of one pixel of the grid
        public float CellSizeInM { get; private set; }
        public int CellsX { get; private set; }
        public int CellsY { get; private set; }
        public T[] Data { get; private set; }
        /// bounding box in project coordinates
        public RectangleF ProjectExtent { get; private set; }

        public Grid()
        {
            this.CellSizeInM = 0.0F;
            this.CellsX = 0;
            this.CellsY = 0;
            this.Data = [];
            this.ProjectExtent = default;
        }

        public Grid(int cellsX, int cellsY, float cellSize)
            : this()
        {
            this.Setup(cellsX, cellsY, cellSize);
        }


        public Grid(RectangleF extent, float cellSize)
            : this()
        {
            this.Setup(extent, cellSize);
        }

        public Grid(Grid<T> other)
            : this()
        {
            if (other.Data == null)
            {
                throw new ArgumentOutOfRangeException(nameof(other));
            }

            this.Setup(other.ProjectExtent, other.CellSizeInM);
            Array.Copy(other.Data, 0, this.Data, 0, other.Data.Length);
        }

        // returns the number of elements of the grid
        public int CellCount 
        { 
            get { return this.Data.Length; }
        }

        public T this[int index]
        {
            get { return this.Data[index]; }
            set { this.Data[index] = value; }
        }

        public T this[int indexX, int indexY]
        {
            get { return this.Data[indexY * this.CellsX + indexX]; }
            set { this.Data[indexY * this.CellsX + indexX] = value; }
        }

        // TOOD: remove to force use of safer index translation APIs on Landscape
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

        public T this[float projectX, float projectY]
        {
            get { return this[this.GetCellXYIndex(projectX, projectY)]; }
            set { this[this.GetCellXYIndex(projectX, projectY)] = value; }
        }

        public T this[Point indexXY]
        {
            get { return this[indexXY.X, indexXY.Y]; }
            set { this[indexXY.X, indexXY.Y] = value; }
        }

        public T this[PointF projectCoordinate]
        {
            get { return this[this.GetCellXYIndex(projectCoordinate)]; }
            set { this[this.GetCellXYIndex(projectCoordinate)] = value; }
        }

        public bool Contains(float projectX, float projectY)
        {
            return (projectX >= this.ProjectExtent.X) && 
                   (projectY >= this.ProjectExtent.Y) &&
                   (projectX < this.ProjectExtent.X + this.ProjectExtent.Width) && 
                   (projectY < this.ProjectExtent.Y + this.ProjectExtent.Height);
        }

        // return true if index is within the grid
        public bool Contains(int indexX, int indexY)
        {
            return (indexX >= 0) && (indexX < this.CellsX) && (indexY >= 0) && (indexY < this.CellsY);
        }

        public bool Contains(Point projectCoordinate)
        {
            return this.Contains(projectCoordinate.X, projectCoordinate.Y);
        }

        public bool Contains(PointF indexXY)
        {
            return this.Contains(indexXY.X, indexXY.Y);
        }

        public PointF GetCellProjectCentroid(Point cellIndexXY)
        {
            return new PointF((cellIndexXY.X + 0.5F) * this.CellSizeInM + this.ProjectExtent.X, 
                              (cellIndexXY.Y + 0.5F) * this.CellSizeInM + this.ProjectExtent.Y);
        }

        /// <summary>
        /// get the center point of the cell given by index in project coordinates
        /// </summary>
        public PointF GetCellProjectCentroid(int cellIndex)
        {
            return this.GetCellProjectCentroid(this.GetCellXYIndex(cellIndex));
        }

        /// <summary>
        /// get the bounding box of the cell given by index in project coordinates
        /// </summary>
        public RectangleF GetCellProjectExtent(Point cellIndexXY) // return coordinates of rect given by @param pos.
        {
            RectangleF extent = new(this.ProjectExtent.X + this.CellSizeInM * cellIndexXY.X, this.ProjectExtent.Y + cellIndexXY.Y * this.CellSizeInM, this.CellSizeInM, this.CellSizeInM);
            return extent;
        }

        // get index of value at position pos (metric)
        public Point GetCellXYIndex(PointF projectCoordinate)
        {
            return this.GetCellXYIndex(projectCoordinate.X, projectCoordinate.Y);
        }

        public Point GetCellXYIndex(float projectX, float projectY)
        {
            // The iLand 1.0 C++ code
            // - places the project origin at the minimum corner of the resource unit rather than the (buffered) light and height grids,
            //   which results negative xy indices for coordinates within the south and west sides of the buffer
            // - incorrectly assumes integer trunction rounds towards minus infinity rather than towards zero, which collapses two rows'
            //   and two columns' light stamping into one row and one column, resulting in double or (for the 0,0 cell) quadruplicate
            //   stamping and errors in tree growth along the south and west boundaries of the project area
            // The implementation in C++ should therefore be
            //   int xIndex = (int)MathF.Floor((projectX - this.ProjectExtent.X) / this.CellSizeInM);
            //   int yIndex = (int)MathF.Floor((projectY - this.ProjectExtent.Y) / this.CellSizeInM);
            // where Floor() rounds towards minus infinity.
            //
            // In the C# implementation negative project coordinates do not occur as the project coordinate system's origin is placed
            // at the light and height grids' (buffered) origin instead of the resource unit grid's origin. Integer truncation, rather
            // than Floor(), can therefore be used.
            Debug.Assert((projectX >= 0.0F) && (projectY >= 0.0F));
            int xIndex = (int)((projectX - this.ProjectExtent.X) / this.CellSizeInM);
            int yIndex = (int)((projectY - this.ProjectExtent.Y) / this.CellSizeInM);
            return new Point(xIndex, yIndex);
        }

        /// get index (x/y) of the (linear) index 'index' (0..count-1)
        public Point GetCellXYIndex(int index)
        {
            return new Point(index % this.CellsX, index / this.CellsX);
        }

        public bool IsIndexValid(Point pos)
        {
            return this.IsIndexValid(pos.X, pos.Y); 
        }

        /// <returns>true if position is within the grid</returns>
        public bool IsIndexValid(int x, int y)
        {
            return (x >= 0) && (x < this.CellsX) && (y >= 0) && (y < this.CellsY);
        }

        public bool IsSetup()
        { 
            return this.Data.Length > 0; 
        }

        public int IndexXYToIndex(int indexX, int indexY) 
        {
            // get the 0-based index of the cell with indices ix and iy.
            return indexY * this.CellsX + indexX; 
        }

        public int IndexXYToIndex(Point indexXY) 
        {
            // get the 0-based index of the cell at 'pos'.
            return indexXY.Y * this.CellsX + indexXY.X; 
        }

        public void CopyFrom(Grid<T> other)
        {
            if ((this.IsSetup() == false) || (other.IsSetup() == false))
            {
                throw new NotSupportedException("Either target or destination grid is not setup.");
            }
            if ((this.CellSizeInM != other.CellSizeInM) || (this.CellsX != other.CellsX) || (this.CellsY != other.CellsY) || (other.CellCount != this.CellCount))
            {
                throw new ArgumentOutOfRangeException(nameof(other));
            }
            this.ProjectExtent = other.ProjectExtent;
            Array.Copy(other.Data, 0, this.Data, 0, other.Data.Length);
        }

        public void Fill(T value)
        {
            if (this.IsSetup() == false)
            {
                throw new NotSupportedException();
            }
            this.Data.AsSpan().Fill(value);
        }

        public void Fill(int startIndexX, int startIndexY, int sizeX, int sizeY, T value)
        {
            int endY = startIndexY + sizeY + 1;
            for (int indexY = startIndexY; indexY < endY; ++indexY)
            {
                int index = this.IndexXYToIndex(startIndexX, startIndexY);
                this.Data.AsSpan().Slice(index, sizeX).Fill(value);
            }
        }

        public float GetCenterToCenterDistance(Point cellIndexXY1, Point cellIndexXY2)
        {
            PointF centroid1 = this.GetCellProjectCentroid(cellIndexXY1);
            PointF centroid2 = this.GetCellProjectCentroid(cellIndexXY2);
            float deltaX = centroid1.X - centroid2.X;
            float deltaY = centroid1.Y - centroid2.Y;
            float distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
            return distance;
        }

        public void Setup(Grid<T> source)
        {
            this.Setup(source.ProjectExtent, source.CellSizeInM);
        }

        public void Setup(RectangleF projectExtent, float cellSizeInM)
        {
            this.ProjectExtent = projectExtent;

            // if needed pad for numerical error
            int cellsX = (int)(projectExtent.Width / cellSizeInM);
            if (this.ProjectExtent.X + cellSizeInM * cellsX < projectExtent.X + projectExtent.Width)
            {
                ++cellsX;
            }
            int cellsY = (int)(projectExtent.Height / cellSizeInM);
            if (this.ProjectExtent.Y + cellSizeInM * cellsY < projectExtent.Y + projectExtent.Height)
            {
                ++cellsY;
            }

            this.Setup(cellsX, cellsY, cellSizeInM); // validates cellsX, cellsY, and cellSizeInM
        }

        public void Setup(int cellsX, int cellsY, float cellSizeInM)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(cellsX, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(cellsY, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(cellSizeInM, 0.0F);

            // reuse the data array that's already been allocated if it's large enough
            // If needed, shrinkage of an existing data array can be supported.
            int newCount = cellsX * cellsY;
            if (newCount > this.CellCount)
            {
                this.Data = new T[newCount]; // for now leave at default value, GC.AllocateUninitializedArray() could be used in some cases
            }

            this.CellSizeInM = cellSizeInM;
            this.CellsX = cellsX;
            this.CellsY = cellsY;
            this.ProjectExtent = new(this.ProjectExtent.X, this.ProjectExtent.Y, cellSizeInM * cellsX, cellSizeInM * cellsY);
        }

        /// dumps a Grid<T> to a wideform CSV
        /// rows will be y-lines, columns x-values
        //public string ToCsv()
        //{
        //    StringBuilder csvBuilder = new();
        //    csvBuilder.AppendLine("x_m,y_m,value"); // wrong if value overrides ToString() and returns multiple values but OK for now
        //    for (int xIndex = 0; xIndex < this.SizeX; ++xIndex)
        //    {
        //        for (int yIndex = 0; yIndex < this.SizeY; ++yIndex)
        //        {
        //            PointF cellCenter = this.GetCellCentroid(new Point(xIndex, yIndex));
        //            csvBuilder.AppendLine(cellCenter.X + "," + cellCenter.Y + "," + this[xIndex, yIndex]!.ToString());
        //        }
        //    }
        //    return csvBuilder.ToString();
        //}
    }
}
