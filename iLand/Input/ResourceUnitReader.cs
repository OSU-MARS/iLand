using iLand.Extensions;
using System;
using System.Drawing;

namespace iLand.Input
{
    public class ResourceUnitReader
    {
        protected float MaximumCenterCoordinateX { get; set; }
        protected float MaximumCenterCoordinateY { get; set; }
        protected float MinimumCenterCoordinateX { get; set; }
        protected float MinimumCenterCoordinateY { get; set; }

        public int Count { get; set; }
        public ResourceUnitEnvironment[] Environments { get; private set; }

        protected ResourceUnitReader()
        {
            this.MaximumCenterCoordinateX = Single.MinValue;
            this.MaximumCenterCoordinateY = Single.MinValue;
            this.MinimumCenterCoordinateX = Single.MaxValue;
            this.MinimumCenterCoordinateY = Single.MaxValue;

            this.Environments = Array.Empty<ResourceUnitEnvironment>();
        }

        public int Capacity
        {
            get { return this.Environments.Length; }
        }

        public RectangleF GetBoundingBox()
        {
            float resourceUnitSize = Constant.Grid.ResourceUnitSizeInM;
            float x = this.MinimumCenterCoordinateX - 0.5F * resourceUnitSize;
            float y = this.MinimumCenterCoordinateY - 0.5F * resourceUnitSize;
            float width = this.MaximumCenterCoordinateX - this.MinimumCenterCoordinateX + resourceUnitSize;
            float height = this.MaximumCenterCoordinateY - this.MinimumCenterCoordinateY + resourceUnitSize;
            return new RectangleF(x, y, width, height);
        }

        public void Resize(int newSize)
        {
            this.Environments = this.Environments.Resize(newSize);
        }
    }
}
