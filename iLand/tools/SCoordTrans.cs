using System;

namespace iLand.tools
{
    internal class SCoordTrans
    {
        public double RotationAngle;
        public double sinRotate, cosRotate;
        public double sinRotateReverse, cosRotateReverse;
        public double offsetX, offsetY, offsetZ;

        public SCoordTrans()
        { 
            setupTransformation(0.0, 0.0, 0.0, 0.0); 
        }
        
        public void setupTransformation(double new_offsetx, double new_offsety, double new_offsetz, double angle_degree)
        {
            offsetX = new_offsetx;
            offsetY = new_offsety;
            offsetZ = new_offsetz;
            RotationAngle = angle_degree * Math.PI / 180.0;
            sinRotate = Math.Sin(RotationAngle);
            cosRotate = Math.Cos(RotationAngle);
            sinRotateReverse = Math.Sin(-RotationAngle);
            cosRotateReverse = Math.Cos(-RotationAngle);
        }
    }
}
