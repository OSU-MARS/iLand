using iLand.World;
using System;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Tree
{
    internal class SaplingCellRunner
    {
        private readonly Landscape landscape;
        private readonly UInt32 standID;
        private readonly GridWindowEnumerator<float> standLightEnumerator;

        public ResourceUnit? RU { get; private set; }

        public SaplingCellRunner(Landscape landscape, UInt32 standID)
        {
            if (landscape.StandRaster == null)
            {
                throw new ArgumentException("Attempt to create a sapling runner on a landscape without stand information.", nameof(landscape));
            }

            this.landscape = landscape;
            this.standID = standID;
            RectangleF standBoundingBox = landscape.StandRaster.GetBoundingBox(standID);
            this.standLightEnumerator = new(landscape.LightGrid, standBoundingBox);

            this.RU = null;
        }

        public PointF GetCurrentProjectCentroid()
        {
            return this.standLightEnumerator.GetCurrentProjectCentroid();
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
                Point cellXYIndex = this.standLightEnumerator.GetCurrentXYIndex();
                if (this.landscape.StandRaster.GetPolygonIDFromLightGridIndex(cellXYIndex) != standID)
                {
                    continue; // pixel does not belong to the target stand
                }
                this.RU = landscape.GetResourceUnit(this.standLightEnumerator.GetCurrentProjectCentroid());
                return this.RU.GetSaplingCell(cellXYIndex);
            }
            return null;
        }
    }
}
