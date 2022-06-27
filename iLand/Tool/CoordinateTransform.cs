using System;

namespace iLand.Tool
{
    internal class CoordinateTransform
    {
        public float SinRotate { get; set; }
        public float CosRotate { get; set; }
        public float SinRotateReverse { get; set; }
        public float CosRotateReverse { get; set; }
        public float OffsetX { get; set; }
        public float OffsetY { get; set; }
        public float OffsetZ { get; set; }

        public CoordinateTransform()
        {
            this.Setup(0.0F, 0.0F, 0.0F, 0.0F);
        }

        // xyRotation: rotation about z axis 
        public void Setup(float offsetX, float offsetY, float offsetZ, float xyRotationInDegrees)
        {
            this.OffsetX = offsetX;
            this.OffsetY = offsetY;
            this.OffsetZ = offsetZ;
            float xyRotationInRadians = Maths.ToRadians(xyRotationInDegrees);
            this.SinRotate = MathF.Sin(xyRotationInRadians);
            this.CosRotate = MathF.Cos(xyRotationInRadians);
            this.SinRotateReverse = MathF.Sin(-xyRotationInRadians);
            this.CosRotateReverse = MathF.Cos(-xyRotationInRadians);
        }
    }
}
