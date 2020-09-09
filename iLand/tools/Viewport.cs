using System;
using System.Drawing;

namespace iLand.tools
{
    /** @class Viewport
      Handles coordinaive transforation between grids (based on real-world metric coordinates).
      The visible part of the grid is defined by the "viewport" (defaults to 100% of the grid).
      The result coordinates are mapped into a "ScreenRect", which is a pixel-based viewing window.
        */
    internal class Viewport
    {
        private bool m_viewAll;
        private Rectangle m_screen;
        private RectangleF m_world;
        private RectangleF m_viewport;
        private PointF m_delta_worldtoscreen;
        private double m_scale_worldtoscreen;

        public Viewport()
        {
            m_viewAll = true;
            m_scale_worldtoscreen = 1.0;
        }

        public Viewport(RectangleF worldrect, Rectangle screenrect)
        {
            setWorldRect(worldrect);
            setScreenRect(screenrect);
            zoomToAll();
        }

        // setters...
        public void setViewRect(RectangleF viewrect)
        {
            m_viewport = viewrect;
        }

        public void setWorldRect(RectangleF worldrect)
        {
            m_world = worldrect;
        }

        private Point center(Rectangle rectangle)
        {
            return new Point(rectangle.Left + (rectangle.Right - rectangle.Left) / 2,
                             rectangle.Bottom + (rectangle.Top - rectangle.Bottom) / 2);
        }

        private PointF center(RectangleF rectangle)
        {
            return new PointF(rectangle.Left + 0.5F * (rectangle.Right - rectangle.Left),
                              rectangle.Bottom + 0.5F * (rectangle.Top - rectangle.Bottom));
        }

        // conversion of length
        public double pixelToMeter(int pixel)
        {
            return pixel / m_scale_worldtoscreen;
        }

        public int meterToPixel(double meter)
        {
            return (int)Math.Round(meter * m_scale_worldtoscreen);
        }

        /// toWorld() converts the pixel-information (e.g. by an mouse event) to the corresponding real world coordinates (defined by viewport).
        public PointF toWorld(Point pixel)
        {
            PointF p = new PointF(pixel.X / (float)m_scale_worldtoscreen + m_delta_worldtoscreen.X,
                                  (m_screen.Height - pixel.Y) / (float)m_scale_worldtoscreen + m_delta_worldtoscreen.Y);
            return p;

        }

        /// toScreen() converts world coordinates in screen coordinates using the defined viewport.
        public Point toScreen(PointF p)
        {
            Point pixel = new Point((int)Math.Round((p.X - m_delta_worldtoscreen.X) * m_scale_worldtoscreen),
                                    m_screen.Height - 1 - (int)Math.Round((p.Y - m_delta_worldtoscreen.Y) * m_scale_worldtoscreen));
            return pixel;
        }

        public RectangleF toWorld(RectangleF screen)
        {
            PointF worldBottomLeft = toWorld(new Point(m_screen.Left, m_screen.Top));
            PointF worldTopRight = toWorld(new Point(m_screen.Right, m_screen.Bottom));
            SizeF worldSize = new SizeF(worldTopRight.X - worldBottomLeft.X, worldTopRight.Y - worldTopRight.Y);
            return new RectangleF(worldBottomLeft.X, worldBottomLeft.Y, worldSize.Width, worldSize.Height);
        }

        public Rectangle toScreen(RectangleF world)
        {
            Point p1 = toScreen(new PointF(world.Left, world.Bottom));
            Point p2 = toScreen(new PointF(world.Right, world.Top));
            Rectangle r = new Rectangle(p1, new Size(p2.X - p1.X, p2.Y - p1.Y));
            return r;
        }

        /// sets the screen rect; this also modifies the viewport.
        public void setScreenRect(Rectangle viewrect)
        {
            if (m_screen != viewrect)
            {
                m_screen = viewrect;
                m_viewport = viewrect;
                zoomToAll();
            }
        }

        /// show the full extent of the world.
        public void zoomToAll()
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
                float world_mid = center(m_world).Y;
                d.Y = world_mid - py_mid / scale;
            }
            else
            {
                d.Y = m_world.Top;
                int px_mid = m_screen.Width / 2;
                float world_mid = center(m_world).X;
                d.X = world_mid - px_mid / scale;
            }
            m_delta_worldtoscreen = d;
            m_scale_worldtoscreen = scale;
            m_viewport = toWorld(m_screen);
        }

        /// zoom using a factor of @p factor. Values > 1 means zoom out, < 1 zoom in. (factor=1 would have no effect).
        /// after zooming, the world-point under the mouse @p screen_point is still under the mouse.
        public void zoomTo(Point screen_point, double factor)
        {
            PointF focus_point = toWorld(screen_point); // point under the mouse

            m_viewport.Width = m_viewport.Width * (float)factor;
            m_viewport.Height = m_viewport.Height * (float)factor;

            m_scale_worldtoscreen /= factor;

            // get scale/delta
            PointF new_focus = toWorld(screen_point);
            m_delta_worldtoscreen.X -= new_focus.X - focus_point.X;
            m_delta_worldtoscreen.Y -= new_focus.Y - focus_point.Y;

            m_viewport = toWorld(m_screen);

            //qDebug() <<"oldf"<< new_focus << "newf" << focus_point << "m_delta" << m_delta_worldtoscreen << "m_scale:" << m_scale_worldtoscreen << "viewport:"<<m_viewport;
        }

        /// move the viewport. @p screen_from and @p screen_to give mouse positions (in pixel) from dragging the mouse.
        public void moveTo(Point screen_from, Point screen_to)
        {
            PointF p1 = toWorld(screen_from);
            PointF p2 = toWorld(screen_to);
            m_delta_worldtoscreen.X -= p2.X - p1.X;
            m_delta_worldtoscreen.Y -= p2.Y - p1.Y;
            // correct the viewport
            m_viewport = toWorld(m_screen);
        }

        /// set 'world_center' as the new center point of the viewport
        public void setViewPoint(PointF world_center, double px_per_meter)
        {
            Point p = toScreen(world_center); // point where world_center would be
            Point target = center(m_screen);
            moveTo(p, target);
            double px_p_m = Math.Max(px_per_meter, 0.001);
            double factor = m_scale_worldtoscreen / px_p_m;
            zoomTo(target, factor);
        }

        public bool isVisible(PointF world_coord)
        {
            return m_viewport.Contains(world_coord);
        }

        public bool isVisible(RectangleF world_rect)
        {
            return m_viewport.Contains(world_rect) || m_viewport.IntersectsWith(world_rect);
        }
    }
}
