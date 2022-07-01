using System;
using System.Drawing;

namespace iLand.World
{
    internal class GridWindowEnumerator<T>
    {
        private readonly Grid<T> grid;
        private readonly int firstIndex;
        private readonly int lastIndex;

        private readonly int columnsNotInWindow;
        private readonly int columnsInWindow;
        private int currentColumnInWindow;

        public int CurrentIndex { get; private set; }

        public GridWindowEnumerator(Grid<T> grid, RectangleF projectExtentToEnumerate)
        {
            Point minimumCoordinate = grid.GetCellXYIndex(projectExtentToEnumerate.X, projectExtentToEnumerate.Y);
            Point maximumCoordinate = grid.GetCellXYIndex(projectExtentToEnumerate.X + projectExtentToEnumerate.Width, projectExtentToEnumerate.Y + projectExtentToEnumerate.Height);
            
            Rectangle cellExtentToRun = new(minimumCoordinate.X, minimumCoordinate.Y, maximumCoordinate.X - minimumCoordinate.X, maximumCoordinate.Y - minimumCoordinate.Y);
            Point upperLeftCellInWindow = new(cellExtentToRun.X, cellExtentToRun.Y);
            Point lowerRightCellInWindow = new(cellExtentToRun.X + cellExtentToRun.Width, cellExtentToRun.Y + cellExtentToRun.Height);

            this.grid = grid;
            this.columnsInWindow = lowerRightCellInWindow.X - upperLeftCellInWindow.X;
            this.firstIndex = grid.IndexXYToIndex(upperLeftCellInWindow.X, upperLeftCellInWindow.Y);
            this.lastIndex = grid.IndexXYToIndex(lowerRightCellInWindow.X - 1, lowerRightCellInWindow.Y - 1);
            if ((this.firstIndex < 0) || (this.lastIndex >= grid.CellCount))
            {
                throw new ArgumentOutOfRangeException(nameof(cellExtentToRun), "Rectangle extends beyond grid.");
            }
            this.columnsNotInWindow = grid.SizeX - this.columnsInWindow;

            this.CurrentIndex = firstIndex - 1; // point to first element -1
            this.currentColumnInWindow = -1;
        }

        public T Current
        {
            get { return this.grid[CurrentIndex]; }
            set { this.grid[CurrentIndex] = value; }
        }

        /// return the coordinates of the cell center point of the current position in the grid.
        public PointF GetPhysicalPosition() { return this.grid.GetCellProjectCentroid(this.grid.GetCellXYIndex(this.CurrentIndex)); }
        /// return the (index) - coordinates of the current position in the grid
        public Point GetCellXYIndex() { return this.grid.GetCellXYIndex(this.CurrentIndex); }

        /// checks if the state of the GridRunner is valid, returns false if out of scope
        public bool IsValid() { return CurrentIndex >= firstIndex && CurrentIndex <= lastIndex; }

        public bool MoveNext()
        {
            if (this.CurrentIndex > this.lastIndex)
            {
                return false;
            }
            ++this.CurrentIndex;
            ++this.currentColumnInWindow;

            if (this.currentColumnInWindow >= this.columnsInWindow)
            {
                this.CurrentIndex += this.columnsNotInWindow; // skip to next line
                this.currentColumnInWindow = 0;
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
        public void GetNeighbors4(T?[] neighborIndicies)
        {
            // north:
            neighborIndicies[0] = this.CurrentIndex + columnsInWindow + columnsNotInWindow > lastIndex ? default : this.grid[CurrentIndex + columnsInWindow + columnsNotInWindow];
            // south:
            neighborIndicies[3] = this.CurrentIndex - (columnsInWindow + columnsNotInWindow) < firstIndex ? default : this.grid[CurrentIndex - (columnsInWindow + columnsNotInWindow)];
            // east / west
            neighborIndicies[1] = this.currentColumnInWindow + 1 < columnsInWindow ? grid[this.CurrentIndex + 1] : default;
            neighborIndicies[2] = this.currentColumnInWindow > 0 ? grid[this.CurrentIndex - 1] : default;
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

        public void Reset()
        {
            this.CurrentIndex = this.firstIndex - 1;
            this.currentColumnInWindow = -1; 
        }

        /// set the internal pointer to the pixel at index 'new_index'. The index is relative to the base grid!
        public void SetPosition(Point cellPosition)
        {
            if (grid.Contains(cellPosition))
            {
                this.CurrentIndex = grid.IndexXYToIndex(cellPosition.X, cellPosition.Y);
            }
            else
            {
                this.CurrentIndex = -1;
            }
        }
    }
}