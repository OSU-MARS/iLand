using System.Drawing;

namespace iLand.core
{
    internal class GridRunner<T>
    {
        private Grid<T> mGrid;
        private int mFirst; // points to the first element of the grid
        private int mLast; // points to the last element of the grid
        private int mCurrent;
        private int mLineLength;
        private int mCols;
        private int mCurrentCol;

        public GridRunner(Grid<T> target_grid, RectangleF rectangle)
        {
            // run over 
            setup(target_grid, rectangle);
        }

        public GridRunner(Grid<T> target_grid) 
        { 
            // run over whole grid
            setup(target_grid, target_grid.rectangle()); 
        }
      
        // BUGBUG: change to Current { get; set; }
        public T current() { return mGrid[mCurrent]; }
        public void setCurrent(T value) { mGrid[mCurrent] = value; }
        /// return the coordinates of the cell center point of the current position in the grid.
        public PointF currentCoord() { return mGrid.cellCenterPoint(mGrid.indexOf(mCurrent)); }
        /// return the (index) - coordinates of the current position in the grid
        public Point currentIndex() { return mGrid.indexOf(mCurrent); }

        /// return the first element
        public int first() { return mFirst; }
        /// return the last element (not one element behind the last element!)
        public int last() { return mLast; }
        /// checks if the state of the GridRunner is valid, returns false if out of scope
        public bool isValid() { return mCurrent >= mFirst && mCurrent <= mLast; }

        ///< to to next element, return null if finished
        /// return the current element, or null
        // BUGBUG: change to MoveNext() and update callers for correct loop termination
        public T next()
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
        public void neighbors4(T[] rArray)
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
        public void neighbors8(T[] rArray)
        {
            neighbors4(rArray);
            // north-east
            int northeastIndex = mCurrent + mCols + mLineLength + 1;
            rArray[4] = mGrid.count() > northeastIndex ? mGrid[northeastIndex] : default;
            // north-west
            int northwestIndex = mCurrent + mCols + mLineLength + 1;
            rArray[5] = mGrid.count() > northwestIndex ? mGrid[northwestIndex] : default;
            // south-east
            int southeastIndex = mCurrent - mCols - mLineLength + 1;
            rArray[6] = southeastIndex >= 0 ? mGrid[southeastIndex]: default;
            // south-west
            int southwestIndex = mCurrent - mCols - mLineLength + 1;
            rArray[7] = southwestIndex >= 0 ? mGrid[southwestIndex]: default;
        }

        public void reset()
        { 
            mCurrent = mFirst - 1; 
            mCurrentCol = -1; 
        }

        /// set the internal pointer to the pixel at index 'new_index'. The index is relative to the base grid!
        public void setPosition(Point new_index)
        {
            if (mGrid.isIndexValid(new_index))
            {
                mCurrent = mGrid.index(new_index.X, new_index.Y);
            }
            else
            {
                mCurrent = -1;
            }
        }

        private void setup(Grid<T> target_grid, Rectangle rectangle)
        {
            Point upper_left = new Point(rectangle.Left, rectangle.Top);
            // due to the strange behavior of Rectangle::bottom() and right():
            Point lower_right = new Point(rectangle.Right, rectangle.Bottom);
            mCurrent = target_grid.index(upper_left.X, upper_left.Y);
            mFirst = mCurrent;
            mCurrent--; // point to first element -1
            mLast = target_grid.index(lower_right.X - 1, lower_right.Y - 1);
            mCols = lower_right.X - upper_left.X; //
            mLineLength = target_grid.sizeX() - mCols;
            mCurrentCol = -1;
            mGrid = target_grid;
            //    qDebug() << "GridRunner: rectangle:" << rectangle
            //             << "upper_left:" << target_grid.cellCenterPoint(target_grid.indexOf(mCurrent))
            //             << "lower_right:" << target_grid.cellCenterPoint(target_grid.indexOf(mLast));
        }

        public void setup(Grid<T> target_grid, RectangleF rectangle_metric)
        {
            Rectangle rect = new Rectangle(target_grid.indexAt(new PointF(rectangle_metric.Left, rectangle_metric.Top)),
                                           new Size(target_grid.indexAt(new PointF(rectangle_metric.Right, rectangle_metric.Bottom))));
            setup(target_grid, rect);
        }
    }
}