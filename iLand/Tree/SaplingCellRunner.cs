using iLand.World;
using System;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Tree
{
    internal class SaplingCellRunner
    {
        private readonly Landscape landscape;
        private readonly int standID;
        private readonly GridWindowEnumerator<float> standLightRunner;

        public ResourceUnit? RU { get; private set; }

        public SaplingCellRunner(Landscape landscape, int standID)
        {
            if (landscape.StandGrid == null)
            {
                throw new ArgumentException("Attempt to create a sapling runner on a landscape without stand information.", nameof(landscape));
            }

            this.landscape = landscape;
            this.standID = standID;
            RectangleF standBoundingBox = landscape.StandGrid.GetBoundingBox(standID);
            this.standLightRunner = new GridWindowEnumerator<float>(landscape.LightGrid, standBoundingBox);

            this.RU = null;
        }

        public PointF CurrentCoordinate()
        {
            return standLightRunner.GetPhysicalPosition();
        }

        // TODO: change to bool MoveNext()
        public SaplingCell? MoveNext()
        {
            Debug.Assert(this.landscape.StandGrid != null);
            while (this.standLightRunner.MoveNext())
            {
                float lightLevel = this.standLightRunner.Current;
                if (lightLevel == 0.0F)
                {
                    return null; // end of the bounding box
                }
                if (this.landscape.StandGrid.GetStandIDFromLightCoordinate(standLightRunner.GetCellPosition()) != standID)
                {
                    continue; // pixel does not belong to the target stand
                }
                this.RU = landscape.GetResourceUnit(standLightRunner.GetPhysicalPosition());
                return this.RU.GetSaplingCell(standLightRunner.GetCellPosition());
            }
            return null;
        }
    }
}
