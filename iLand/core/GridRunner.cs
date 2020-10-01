using System.Drawing;

namespace iLand.Core
{
    internal class GridRunner<T>
    {
        private Grid<T> mGrid;
        private int mFirst;
        private int mLast;

        private int mCurrent;
        private int mLineLength;
        private int mCols;
        private int mCurrentCol;

        public GridRunner(Grid<T> target_grid, RectangleF rectangle)
        {
            // run over 
            Setup(target_grid, rectangle);
        }

        public GridRunner(Grid<T> target_grid) 
        { 
            // run over whole grid
            Setup(target_grid, target_grid.CellExtent()); 
        }
      
        public T Current
        {
            get { return mGrid[mCurrent]; }
            set { mGrid[mCurrent] = value; }
        }

        /// return the coordinates of the cell center point of the current position in the grid.
        public PointF CurrentCoordinate() { return mGrid.GetCellCenterPoint(mGrid.IndexOf(mCurrent)); }
        /// return the (index) - coordinates of the current position in the grid
        public Point CurrentIndex() { return mGrid.IndexOf(mCurrent); }

        /// checks if the state of the GridRunner is valid, returns false if out of scope
        public bool IsValid() { return mCurrent >= mFirst && mCurrent <= mLast; }

        ///< to to next element, return null if finished
        /// return the current element, or null
        // BUGBUG: change to bool MoveNext() and update callers
        public T MoveNext()
        {
            if (mCurrent > mLast)
            {
                return default;
            }
            mCurrent++;
            mCurrentCol++;

            if (mCurrentCol >= mCols)
            {
                mCurrent += mLineLength; // skip to next line
                mCurrentCol = 0;
            }
            if (mCurrent > mLast)
            {
                return default;
            }
            else
            {
                return mGrid[mCurrent];
            }
        }

        /// get pointers the the 4-neighborhood
        /// north, east, west, south
        /// 0-pointers are returned for edge pixels.
        public void Neighbors4(T[] rArray)
        {
            // north:
            rArray[0] = mCurrent + mCols + mLineLength > mLast ? default : mGrid[mCurrent + mCols + mLineLength];
            // south:
            rArray[3] = mCurrent - (mCols + mLineLength) < mFirst ? default : mGrid[mCurrent - (mCols + mLineLength)];
            // east / west
            rArray[1] = mCurrentCol + 1 < mCols ? mGrid[mCurrent + 1] : default;
            rArray[2] = mCurrentCol > 0 ? mGrid[mCurrent - 1] : default;
        }

        /// get pointers to the 8-neighbor-hood
        /// north/east/west/south/NE/NW/SE/SW
        /// 0-pointers are returned for edge pixels.
        public void Neighbors8(T[] rArray)
        {
            Neighbors4(rArray);
            // north-east
            int northeastIndex = mCurrent + mCols + mLineLength + 1;
            rArray[4] = mGrid.Count > northeastIndex ? mGrid[northeastIndex] : default;
            // north-west
            int northwestIndex = mCurrent + mCols + mLineLength + 1;
            rArray[5] = mGrid.Count > northwestIndex ? mGrid[northwestIndex] : default;
            // south-east
            int southeastIndex = mCurrent - mCols - mLineLength + 1;
            rArray[6] = southeastIndex >= 0 ? mGrid[southeastIndex]: default;
            // south-west
            int southwestIndex = mCurrent - mCols - mLineLength + 1;
            rArray[7] = southwestIndex >= 0 ? mGrid[southwestIndex]: default;
        }

        public void Reset()
        { 
            mCurrent = mFirst - 1; 
            mCurrentCol = -1; 
        }

        /// set the internal pointer to the pixel at index 'new_index'. The index is relative to the base grid!
        public void SetPosition(Point new_index)
        {
            if (mGrid.Contains(new_index))
            {
                mCurrent = mGrid.IndexOf(new_index.X, new_index.Y);
            }
            else
            {
                mCurrent = -1;
            }
        }

        private void Setup(Grid<T> target_grid, Rectangle rectangle)
        {
            Point upper_left = new Point(rectangle.Left, rectangle.Top);
            // due to the strange behavior of Rectangle::bottom() and right():
            Point lower_right = new Point(rectangle.Right, rectangle.Bottom);
            mCurrent = target_grid.IndexOf(upper_left.X, upper_left.Y);
            mFirst = mCurrent;
            mCurrent--; // point to first element -1
            mLast = target_grid.IndexOf(lower_right.X - 1, lower_right.Y - 1);
            mCols = lower_right.X - upper_left.X; //
            mLineLength = target_grid.CellsX - mCols;
            mCurrentCol = -1;
            mGrid = target_grid;
            //    qDebug() << "GridRunner: rectangle:" << rectangle
            //             << "upper_left:" << target_grid.cellCenterPoint(target_grid.indexOf(mCurrent))
            //             << "lower_right:" << target_grid.cellCenterPoint(target_grid.indexOf(mLast));
        }

        public void Setup(Grid<T> target_grid, RectangleF rectangle_metric)
        {
            Point topLeft = target_grid.IndexAt(rectangle_metric.Left, rectangle_metric.Top);
            Point bottomRight = target_grid.IndexAt(rectangle_metric.Right, rectangle_metric.Bottom);
            Rectangle rect = new Rectangle(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
            Setup(target_grid, rect);
        }
    }
}