using System.Drawing;

namespace iLand.tools
{
    internal static class PointFExtensions
    {
        public static PointF Add(this PointF point, PointF offset)
        {
            return new PointF(point.X + offset.X, point.Y + offset.Y);
        }
    }
}
