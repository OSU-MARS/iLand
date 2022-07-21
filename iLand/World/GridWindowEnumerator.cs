using System;
using System.Drawing;

namespace iLand.World
{
    internal class GridWindowEnumerator<T>
    {
        private readonly Grid<T> grid;
        private readonly int firstIndex; // index of first cell to enumerate, inclusive
        private readonly int lastIndex; // index of first cell to enumerate, also inclusive

        private readonly int columnsNotInWindow;
        private readonly int columnsInWindow;
        private int currentColumnIndexInWindow;

        public int CurrentIndex { get; private set; }

        public GridWindowEnumerator(Grid<T> grid, RectangleF projectExtentToEnumerate)
        {
            Point minimumCoordinate = grid.GetCellXYIndex(projectExtentToEnumerate.X, projectExtentToEnumerate.Y);
            Point maximumCoordinate = grid.GetCellXYIndex(projectExtentToEnumerate.X + projectExtentToEnumerate.Width, projectExtentToEnumerate.Y + projectExtentToEnumerate.Height);

            Rectangle cellExtentToRun = new(minimumCoordinate.X, minimumCoordinate.Y, maximumCoordinate.X - minimumCoordinate.X, maximumCoordinate.Y - minimumCoordinate.Y);
            Point lowerLeftCellInWindow = new(cellExtentToRun.X, cellExtentToRun.Y);
            Point upperRightCellBeyondWindow = new(cellExtentToRun.X + cellExtentToRun.Width, cellExtentToRun.Y + cellExtentToRun.Height);

            this.grid = grid;
            this.columnsInWindow = upperRightCellBeyondWindow.X - lowerLeftCellInWindow.X;
            this.firstIndex = grid.IndexXYToIndex(lowerLeftCellInWindow.X, lowerLeftCellInWindow.Y);
            this.lastIndex = grid.IndexXYToIndex(upperRightCellBeyondWindow.X - 1, upperRightCellBeyondWindow.Y - 1);
            if ((this.firstIndex < 0) || (this.lastIndex >= grid.CellCount))
            {
                throw new ArgumentOutOfRangeException(nameof(cellExtentToRun), "Rectangle extends beyond grid.");
            }
            this.columnsNotInWindow = grid.SizeX - this.columnsInWindow;

            this.CurrentIndex = firstIndex - 1; // point to first element - 1 for first MoveNext() call
            this.currentColumnIndexInWindow = -1;
        }

        public T Current
        {
            get { return this.grid[this.CurrentIndex]; }
        }

        /// return the (index) - coordinates of the current position in the grid
        public Point GetCurrentXYIndex()
        {
            return this.grid.GetCellXYIndex(this.CurrentIndex);
        }

        /// return the coordinates of the cell center point of the current position in the grid.
        public PointF GetCurrentProjectCentroid() 
        { 
            return this.grid.GetCellProjectCentroid(this.grid.GetCellXYIndex(this.CurrentIndex)); 
        }

        public bool MoveNext()
        {
            if (this.CurrentIndex > this.lastIndex)
            {
                return false;
            }
            ++this.CurrentIndex;
            ++this.currentColumnIndexInWindow;

            if (this.currentColumnIndexInWindow == this.columnsInWindow)
            {
                this.CurrentIndex += this.columnsNotInWindow; // skip to next line
                this.currentColumnIndexInWindow = 0;
            }
            if (this.CurrentIndex > this.lastIndex)
            {
                return false;
            }

            return true;
        }

        /// get pointers the the 4-neighborhood
        /// north, east, west, south
        /// 0-pointers are returned for edge pixels.
        public void GetNeighbors4(T?[] neighborIndices)
        {
            // north:
            neighborIndices[0] = this.CurrentIndex + columnsInWindow + columnsNotInWindow > lastIndex ? default : this.grid[CurrentIndex + columnsInWindow + columnsNotInWindow];
            // south:
            neighborIndices[3] = this.CurrentIndex - (columnsInWindow + columnsNotInWindow) < firstIndex ? default : this.grid[CurrentIndex - (columnsInWindow + columnsNotInWindow)];
            // east / west
            neighborIndices[1] = this.currentColumnIndexInWindow + 1 < columnsInWindow ? grid[this.CurrentIndex + 1] : default;
            neighborIndices[2] = this.currentColumnIndexInWindow > 0 ? grid[this.CurrentIndex - 1] : default;
        }

        /// get pointers to the 8-neighbor-hood
        /// north/east/west/south/NE/NW/SE/SW
        /// 0-pointers are returned for edge pixels.
        public void GetNeighbors8(T?[] neighborIndices)
        {
            this.GetNeighbors4(neighborIndices);
            // north-east
            int northeastIndex = CurrentIndex + columnsInWindow + columnsNotInWindow + 1;
            neighborIndices[4] = grid.CellCount > northeastIndex ? grid[northeastIndex] : default;
            // north-west
            int northwestIndex = CurrentIndex + columnsInWindow + columnsNotInWindow + 1;
            neighborIndices[5] = grid.CellCount > northwestIndex ? grid[northwestIndex] : default;
            // south-east
            int southeastIndex = CurrentIndex - columnsInWindow - columnsNotInWindow + 1;
            neighborIndices[6] = southeastIndex >= 0 ? grid[southeastIndex]: default;
            // south-west
            int southwestIndex = CurrentIndex - columnsInWindow - columnsNotInWindow + 1;
            neighborIndices[7] = southwestIndex >= 0 ? grid[southwestIndex]: default;
        }
    }
}