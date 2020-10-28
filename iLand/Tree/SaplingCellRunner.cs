using iLand.Simulation;
using iLand.World;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Tree
{
    internal class SaplingCellRunner
    {
        private readonly Model mModel;
        private readonly MapGrid mStandGrid;
        private readonly int mStandID;
        private readonly GridWindowEnumerator<float> standLightRunner;

        public ResourceUnit RU { get; private set; }

        public SaplingCellRunner(Model model, int standID, MapGrid standGrid)
        {
            this.mModel = model;
            this.mStandID = standID;
            this.mStandGrid = standGrid ?? model.StandGrid;
            RectangleF standBoundingBox = mStandGrid.GetBoundingBox(standID);
            this.standLightRunner = new GridWindowEnumerator<float>(model.LightGrid, standBoundingBox);

            this.RU = null;
        }

        public PointF CurrentCoordinate()
        {
            return standLightRunner.GetPhysicalPosition();
        }

        // TODO: change to bool MoveNext()
        public SaplingCell MoveNext()
        {
            if (standLightRunner == null)
            {
                return null;
            }
            while (standLightRunner.MoveNext())
            {
                float n = standLightRunner.Current;
                if (n == 0.0F)
                {
                    return null; // end of the bounding box
                }
                if (mStandGrid.GetStandIDFromLightCoordinate(standLightRunner.GetCellPosition()) != mStandID)
                {
                    continue; // pixel does not belong to the target stand
                }
                this.RU = mModel.GetResourceUnit(standLightRunner.GetPhysicalPosition());
                SaplingCell saplingCell = null;
                if (this.RU != null)
                {
                    saplingCell = RU.SaplingCell(standLightRunner.GetCellPosition());
                }
                if (saplingCell != null)
                {
                    return saplingCell;
                }
                Debug.WriteLine("MoveNext(): unexected missing SaplingCell.");
                return null; // TODO: is this correct?
            }
            return null;
        }
    }
}
