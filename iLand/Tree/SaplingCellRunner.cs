﻿using iLand.World;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Tree
{
    internal class SaplingCellRunner
    {
        private readonly Landscape landscape;
        private readonly int standID;
        private readonly GridWindowEnumerator<float> standLightRunner;

        public ResourceUnit RU { get; private set; }

        public SaplingCellRunner(Landscape landscape, int standID)
        {
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
                if (landscape.StandGrid.GetStandIDFromLightCoordinate(standLightRunner.GetCellPosition()) != standID)
                {
                    continue; // pixel does not belong to the target stand
                }
                this.RU = landscape.GetResourceUnit(standLightRunner.GetPhysicalPosition());
                SaplingCell saplingCell = null;
                if (this.RU != null)
                {
                    saplingCell = this.RU.GetSaplingCell(standLightRunner.GetCellPosition());
                }
                if (saplingCell != null)
                {
                    return saplingCell;
                }
                Debug.Fail("Unexected missing SaplingCell.");
                return null; // TODO: is this correct?
            }
            return null;
        }
    }
}
