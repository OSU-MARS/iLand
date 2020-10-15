using System;

namespace iLand.World
{
    internal class CoordinateTransform
    {
        private double rotationAngle;

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

        public void SetupTransformation(double new_offsetx, double new_offsety, double new_offsetz, double angle_degree)
        {
            OffsetX = new_offsetx;
            OffsetY = new_offsety;
            OffsetZ = new_offsetz;
            rotationAngle = angle_degree * Math.PI / 180.0;
            SinRotate = Math.Sin(rotationAngle);
            CosRotate = Math.Cos(rotationAngle);
            SinRotateReverse = Math.Sin(-rotationAngle);
            CosRotateReverse = Math.Cos(-rotationAngle);
        }
    }
}
