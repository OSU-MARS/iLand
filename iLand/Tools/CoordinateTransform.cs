using System;

namespace iLand.Tools
{
    internal class CoordinateTransform
    {
        private float rotationInRadians;

        public float SinRotate { get; set; }
        public float CosRotate { get; set; }
        public float SinRotateReverse { get; set; }
        public float CosRotateReverse { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float OffsetZ { get; set; }

        public CoordinateTransform()
        {
            this.SetupTransformation(0.0F, 0.0F, 0.0F, 0.0F);
        }

        public void SetupTransformation(float offsetX, float offsetY, float offsetZ, float rotationInDegrees)
        {
            this.OffsetX = offsetX;
            this.OffsetY = offsetY;
            this.OffsetZ = offsetZ;
            this.rotationInRadians = Maths.ToRadians(rotationInDegrees);
            this.SinRotate = MathF.Sin(rotationInRadians);
            this.CosRotate = MathF.Cos(rotationInRadians);
            this.SinRotateReverse = MathF.Sin(-rotationInRadians);
            this.CosRotateReverse = MathF.Cos(-rotationInRadians);
        }
    }
}
