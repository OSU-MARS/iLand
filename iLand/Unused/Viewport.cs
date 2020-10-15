using System;
using System.Drawing;

namespace iLand.Tools
{
    /** @class Viewport
      Handles coordinaive transforation between grids (based on real-world metric coordinates).
      The visible part of the grid is defined by the "viewport" (defaults to 100% of the grid).
      The result coordinates are mapped into a "ScreenRect", which is a pixel-based viewing window.
        */
    internal class Viewport
    {
        private Rectangle m_screen;
        private RectangleF m_world;
        private RectangleF m_viewport;
        private PointF m_delta_worldtoscreen;
        private double m_scale_worldtoscreen;

        public Viewport()
        {
            m_scale_worldtoscreen = 1.0;
        }

        public Viewport(RectangleF worldrect, Rectangle screenrect)
        {
            SetWorldRect(worldrect);
            SetScreenRect(screenrect);
            ZoomToAll();
        }

        // setters...
        public void SetViewRect(RectangleF viewrect)
        {
            m_viewport = viewrect;
        }

        public void SetWorldRect(RectangleF worldrect)
        {
            m_world = worldrect;
        }

        private Point Center(Rectangle rectangle)
        {
            return new Point(rectangle.Left + (rectangle.Right - rectangle.Left) / 2,
                             rectangle.Bottom + (rectangle.Top - rectangle.Bottom) / 2);
        }

        private PointF Center(RectangleF rectangle)
        {
            return new PointF(rectangle.Left + 0.5F * (rectangle.Right - rectangle.Left),
                              rectangle.Bottom + 0.5F * (rectangle.Top - rectangle.Bottom));
        }

        // conversion of length
        public double PixelToMeter(int pixel)
        {
            return pixel / m_scale_worldtoscreen;
        }

        public int MeterToPixel(double meter)
        {
            return (int)Math.Round(meter * m_scale_worldtoscreen);
        }

        /// toWorld() converts the pixel-information (e.g. by an mouse event) to the corresponding real world coordinates (defined by viewport).
        public PointF ToWorld(Point pixel)
        {
            PointF p = new PointF(pixel.X / (float)m_scale_worldtoscreen + m_delta_worldtoscreen.X,
                                  (m_screen.Height - pixel.Y) / (float)m_scale_worldtoscreen + m_delta_worldtoscreen.Y);
            return p;

        }

        /// toScreen() converts world coordinates in screen coordinates using the defined viewport.
        public Point ToScreen(PointF p)
        {
            Point pixel = new Point((int)Math.Round((p.X - m_delta_worldtoscreen.X) * m_scale_worldtoscreen),
                                    m_screen.Height - 1 - (int)Math.Round((p.Y - m_delta_worldtoscreen.Y) * m_scale_worldtoscreen));
            return pixel;
        }

        public RectangleF ToWorld()
        {
            PointF worldBottomLeft = ToWorld(new Point(m_screen.Left, m_screen.Top));
            PointF worldTopRight = ToWorld(new Point(m_screen.Right, m_screen.Bottom));
            return new RectangleF(worldBottomLeft.X, worldBottomLeft.Y, worldTopRight.X - worldBottomLeft.X, worldTopRight.Y - worldTopRight.Y);
        }

        public Rectangle ToScreen(RectangleF world)
        {
            Point p1 = ToScreen(new PointF(world.Left, world.Bottom));
            Point p2 = ToScreen(new PointF(world.Right, world.Top));
            Rectangle r = new Rectangle(p1.X, p2.Y, p2.X - p1.X, p2.Y - p1.Y);
            return r;
        }

        /// sets the screen rect; this also modifies the viewport.
        public void SetScreenRect(Rectangle viewrect)
        {
            if (m_screen != viewrect)
            {
                m_screen = viewrect;
                m_viewport = viewrect;
                ZoomToAll();
            }
        }

        /// show the full extent of the world.
        public void ZoomToAll()
        {
            // calculate move/scale so that world-rect maps entirely onto screen
            float scale_x = m_screen.Width / m_world.Width; // pixel per meter in x
            float scale_y = m_screen.Height / m_world.Height; // pixel per meter in y
            float scale = Math.Min(scale_x, scale_y);
            PointF d = new PointF();
            if (scale_x < scale_y)
            {
                // x-axis fills the screen; center in y-axis
                d.X = m_world.Left;
                int py_mid = m_screen.Height / 2;
                float world_mid = Center(m_world).Y;
                d.Y = world_mid - py_mid / scale;
            }
            else
            {
                d.Y = m_world.Top;
                int px_mid = m_screen.Width / 2;
                float world_mid = Center(m_world).X;
                d.X = world_mid - px_mid / scale;
            }
            m_delta_worldtoscreen = d;
            m_scale_worldtoscreen = scale;
            m_viewport = ToWorld();
        }

        /// zoom using a factor of @p factor. Values > 1 means zoom out, < 1 zoom in. (factor=1 would have no effect).
        /// after zooming, the world-point under the mouse @p screen_point is still under the mouse.
        public void ZoomTo(Point screen_point, double factor)
        {
            PointF focus_point = ToWorld(screen_point); // point under the mouse

            m_viewport.Width *= (float)factor;
            m_viewport.Height *= (float)factor;

            m_scale_worldtoscreen /= factor;

            // get scale/delta
            PointF new_focus = ToWorld(screen_point);
            m_delta_worldtoscreen.X -= new_focus.X - focus_point.X;
            m_delta_worldtoscreen.Y -= new_focus.Y - focus_point.Y;

            m_viewport = ToWorld();

            //qDebug() <<"oldf"<< new_focus << "newf" << focus_point << "m_delta" << m_delta_worldtoscreen << "m_scale:" << m_scale_worldtoscreen << "viewport:"<<m_viewport;
        }

        /// move the viewport. @p screen_from and @p screen_to give mouse positions (in pixel) from dragging the mouse.
        public void MoveTo(Point screen_from, Point screen_to)
        {
            PointF p1 = ToWorld(screen_from);
            PointF p2 = ToWorld(screen_to);
            m_delta_worldtoscreen.X -= p2.X - p1.X;
            m_delta_worldtoscreen.Y -= p2.Y - p1.Y;
            // correct the viewport
            m_viewport = ToWorld();
        }

        /// set 'world_center' as the new center point of the viewport
        public void SetViewPoint(PointF world_center, double px_per_meter)
        {
            Point p = ToScreen(world_center); // point where world_center would be
            Point target = Center(m_screen);
            MoveTo(p, target);
            double px_p_m = Math.Max(px_per_meter, 0.001);
            double factor = m_scale_worldtoscreen / px_p_m;
            ZoomTo(target, factor);
        }

        public bool IsVisible(PointF world_coord)
        {
            return m_viewport.Contains(world_coord);
        }

        public bool IsVisible(RectangleF world_rect)
        {
            return m_viewport.Contains(world_rect) || m_viewport.IntersectsWith(world_rect);
        }
    }
}
