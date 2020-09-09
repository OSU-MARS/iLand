using iLand.tools;
using System.Diagnostics;
using System.Drawing;

namespace iLand.core
{
    internal class SaplingCellRunner
    {
        private MapGrid mStandGrid;

        private GridRunner<float> mRunner;
        private ResourceUnit mRU;
        private int mStandId;

        public ResourceUnit ru() { return mRU; }

        public SaplingCellRunner(int stand_id, MapGrid stand_grid)
        {
            mRU = null;
            mStandId = stand_id;
            mStandGrid = stand_grid != null ? stand_grid : GlobalSettings.instance().model().standGrid();
            RectangleF box = mStandGrid.boundingBox(stand_id);
            mRunner = new GridRunner<float>(GlobalSettings.instance().model().grid(), box);
        }

        public SaplingCell next()
        {
            if (mRunner == null)
            {
                return null;
            }
            for (mRunner.next(); mRunner.isValid(); mRunner.next())
            {
                float n = mRunner.current();
                if (n == 0.0F)
                {
                    return null; // end of the bounding box
                }
                if (mStandGrid.standIDFromLIFCoord(mRunner.currentIndex()) != mStandId)
                {
                    continue; // pixel does not belong to the target stand
                }
                mRU = GlobalSettings.instance().model().ru(mRunner.currentCoord());
                SaplingCell sc = null;
                if (mRU != null)
                {
                    sc = mRU.saplingCell(mRunner.currentIndex());
                }
                if (sc != null)
                {
                    return sc;
                }
                Debug.WriteLine("next(): unexected missing SaplingCell!");
                return null; // TODO: is this correct?
            }
            return null;
        }

        public PointF currentCoord()
        {
            return mRunner.currentCoord();
        }
    }
}
