using iLand.tools;
using System.Diagnostics;
using System.Drawing;

namespace iLand.core
{
    internal class SaplingCellRunner
    {
        private readonly MapGrid mStandGrid;

        private readonly GridRunner<float> mRunner;
        private readonly int mStandId;

        public ResourceUnit RU { get; private set; }

        public SaplingCellRunner(int stand_id, MapGrid stand_grid)
        {
            RU = null;
            mStandId = stand_id;
            mStandGrid = stand_grid ?? GlobalSettings.Instance.Model.StandGrid;
            RectangleF box = mStandGrid.BoundingBox(stand_id);
            mRunner = new GridRunner<float>(GlobalSettings.Instance.Model.LightGrid, box);
        }

        public PointF CurrentCoordinate()
        {
            return mRunner.CurrentCoordinate();
        }

        public SaplingCell MoveNext()
        {
            if (mRunner == null)
            {
                return null;
            }
            for (mRunner.MoveNext(); mRunner.IsValid(); mRunner.MoveNext())
            {
                float n = mRunner.Current;
                if (n == 0.0F)
                {
                    return null; // end of the bounding box
                }
                if (mStandGrid.StandIDFromLifCoord(mRunner.CurrentIndex()) != mStandId)
                {
                    continue; // pixel does not belong to the target stand
                }
                RU = GlobalSettings.Instance.Model.GetResourceUnit(mRunner.CurrentCoordinate());
                SaplingCell sc = null;
                if (RU != null)
                {
                    sc = RU.SaplingCell(mRunner.CurrentIndex());
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
    }
}
