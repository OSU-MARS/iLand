using System.Drawing;

namespace iLand.tools
{
    internal static class RectangleFExtensions
    {
        public static PointF BottomRight(this RectangleF rectangle)
        {
            return new PointF(rectangle.Right, rectangle.Bottom);
        }

        public static PointF Center(this RectangleF rectangle)
        {
            return new PointF(rectangle.X + 0.5F * rectangle.Width, rectangle.Y + 0.5F * rectangle.Height);
        }

        public static PointF TopLeft(this RectangleF rectangle)
        {
            return new PointF(rectangle.Left, rectangle.Top);
        }
    }
}
