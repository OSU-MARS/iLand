using System.Drawing;

namespace iLand.World
{
    internal static class PointExtensions
    {
        public static Point Add(this Point point, Point offset)
        {
            return new Point(point.X + offset.X, point.Y + offset.Y);
        }

        public static Point Subtract(this Point point, int offset)
        {
            return new Point(point.X - offset, point.Y - offset);
        }

        public static Point Subtract(this Point point, Point other)
        {
            return new Point(point.X - other.X, point.Y - other.Y);
        }
    }
}
