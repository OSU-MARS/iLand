using iLand.Simulation;
using iLand.Tools;
using iLand.World;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Trees
{
    internal class SaplingCellRunner
    {
        private readonly Model mModel;
        private readonly MapGrid mStandGrid;
        private readonly int mStandID;
        private readonly GridRunner<float> mRunner;

        public ResourceUnit RU { get; private set; }

        public SaplingCellRunner(int standID, MapGrid standGrid, Model model)
        {
            this.mModel = model;
            this.mStandID = standID;
            this.mStandGrid = standGrid ?? model.StandGrid;
            RectangleF box = mStandGrid.BoundingBox(standID);
            this.mRunner = new GridRunner<float>(model.LightGrid, box);

            this.RU = null;
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
                if (mStandGrid.StandIDFromLightCoordinate(mRunner.CurrentIndex()) != mStandID)
                {
                    continue; // pixel does not belong to the target stand
                }
                RU = mModel.GetResourceUnit(mRunner.CurrentCoordinate());
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
