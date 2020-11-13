namespace iLand.Tools
{
    public class Vector3D
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3D()
        {
            this.X = 0.0F;
            this.Y = 0.0F;
            this.Z = 0.0F;
        }

        public Vector3D(float x, float y, float z)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
        }
    }
}
