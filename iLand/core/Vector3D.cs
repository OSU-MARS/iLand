namespace iLand.core
{
    internal class Vector3D
    {
        private double mX;
        private double mY;
        private double mZ;

        public Vector3D()
        {
            mX = 0.0;
            mY = 0.0;
            mZ = 0.0;
        }

        public Vector3D(double x, double y, double z)
        {
            mX = x;
            mY = y;
            mZ = z;
        }

        public double x() { return mX; } ///< get x-coordinate
        public double y() { return mY; } ///< get y-coordinate
        public double z() { return mZ; } ///< get z-coordinate
        // set variables
        public void setX(double x) { mX = x; } ///< set value of the x-coordinate
        public void setY(double y) { mY = y; } ///< set value of the y-coordinate
        public void setZ(double z) { mZ = z; } ///< set value of the z-coordinate
    }
}
