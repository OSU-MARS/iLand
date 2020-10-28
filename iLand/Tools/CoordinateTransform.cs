using System;

namespace iLand.Tools
{
    internal class CoordinateTransform
    {
        private double rotationInRadians;

        public double SinRotate { get; set; }
        public double CosRotate { get; set; }
        public double SinRotateReverse { get; set; }
        public double CosRotateReverse { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double OffsetZ { get; set; }

        public CoordinateTransform()
        {
            this.SetupTransformation(0.0, 0.0, 0.0, 0.0);
        }

        public void SetupTransformation(double offsetX, double offsetY, double offsetZ, double rotationInDegrees)
        {
            this.OffsetX = offsetX;
            this.OffsetY = offsetY;
            this.OffsetZ = offsetZ;
            this.rotationInRadians = Maths.ToRadians(rotationInDegrees);
            this.SinRotate = Math.Sin(rotationInRadians);
            this.CosRotate = Math.Cos(rotationInRadians);
            this.SinRotateReverse = Math.Sin(-rotationInRadians);
            this.CosRotateReverse = Math.Cos(-rotationInRadians);
        }
    }
}
