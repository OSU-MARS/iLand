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
        private readonly GridWindowEnumerator<float> standLightEnumerator;

        public ResourceUnit? RU { get; private set; }

        public SaplingCellRunner(Landscape landscape, int standID)
        {
            if (landscape.StandRaster == null)
            {
                throw new ArgumentException("Attempt to create a sapling runner on a landscape without stand information.", nameof(landscape));
            }

            this.landscape = landscape;
            this.standID = standID;
            RectangleF standBoundingBox = landscape.StandRaster.GetBoundingBox(standID);
            this.standLightEnumerator = new GridWindowEnumerator<float>(landscape.LightGrid, standBoundingBox);

            this.RU = null;
        }

        public PointF CurrentCoordinate()
        {
            return standLightEnumerator.GetPhysicalPosition();
        }

        // TODO: change to bool MoveNext()?
        public SaplingCell? MoveNext()
        {
            Debug.Assert(this.landscape.StandRaster != null);
            while (this.standLightEnumerator.MoveNext())
            {
                float lightLevel = this.standLightEnumerator.Current;
                if (lightLevel == 0.0F)
                {
                    return null; // end of the bounding box
                }
                Point cellXYIndex = this.standLightEnumerator.GetCellXYIndex();
                if (this.landscape.StandRaster.GetPolygonIDFromLightGridIndex(cellXYIndex) != standID)
                {
                    continue; // pixel does not belong to the target stand
                }
                this.RU = landscape.GetResourceUnit(this.standLightEnumerator.GetPhysicalPosition());
                return this.RU.GetSaplingCell(cellXYIndex);
            }
            return null;
        }
    }
}
