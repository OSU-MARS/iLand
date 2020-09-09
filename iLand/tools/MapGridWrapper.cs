using iLand.core;
using System;
using System.Diagnostics;
using System.Drawing;

namespace iLand.tools
{
    internal class MapGridWrapper
    {
        private MapGrid mMap;
        private bool mCreated;
        private object scriptOutput = null;

        public MapGrid map() { return mMap; } ///< acccess for C++ classes

        public static void addToScriptEngine(QJSEngine engine)
        {
            // about this kind of scripting magic see: http://qt.nokia.com/developer/faqs/faq.2007-06-25.9557303148
            //QJSValue cc_class = engine.scriptValueFromQMetaObject<MapGridWrapper>();
            // the script name for the object is "Map".
            // TODO: solution for creating objects!!!
            MapGridWrapper mgw = new MapGridWrapper();
            QJSValue mgw_cls = engine.newQObject(mgw);
            engine.globalObject().setProperty("Map", mgw_cls);
        }

        public MapGridWrapper(object parent = null)
        {
            mCreated = false;
            if (GlobalSettings.instance().model() == null)
            {
                return;
            }
            mMap = GlobalSettings.instance().model().standGrid();
        }

        public void load(string file_name)
        {
            mMap = new MapGrid(file_name);
            mCreated = true;
        }

        public bool isValid()
        {
            return mMap.isValid();
        }

        public void saveAsImage(string file)
        {
            Debug.WriteLine("not implemented"); // BUGBUG
        }

        public void paint(double min_value, double max_value)
        {
            //gridToImage(mMap.grid(), false, min_value, max_value).save(file_name);
            if (mMap != null)
            {
                if (GlobalSettings.instance().controller() != null)
                {
                    GlobalSettings.instance().controller().paintMap(mMap, min_value, max_value);
                }
            }
        }

        public void clear()
        {
            if (!mCreated)
            {
                // create a empty map
                mMap = new MapGrid();
                mMap.createEmptyGrid();
                mCreated = true;
            }
            mMap.grid().initialize(0); // clear all data and set to 0
        }

        public void clearProjectArea()
        {
            if (!mCreated)
            {
                // create a empty map
                mMap = new MapGrid();
                mMap.createEmptyGrid();
                mCreated = true;
            }
            MapGrid stand_grid = GlobalSettings.instance().model().standGrid();
            if (stand_grid == null)
            {
                Debug.WriteLine("clearProjectArea: no valid stand grid to copy from!");
                return;
            }
            for (int index = 0; index < stand_grid.grid().count(); ++index)
            {
                mMap.grid()[index] = stand_grid.grid()[index] < 0 ? stand_grid.grid()[index] : 0;
            }
        }

        public void createStand(int stand_id, string paint_function, bool wrap_around)
        {
            if (mMap == null)
            {
                throw new NotSupportedException("no valid map to paint on");
            }
            Expression expr = new Expression(paint_function);
            expr.setCatchExceptions(true);
            double x_var = expr.addVar("x");
            double y_var = expr.addVar("y");
            if (!wrap_around)
            {
                // now loop over all cells ...
                for (int p = 0; p < mMap.grid().count(); ++p)
                {
                    Point pt = mMap.grid().indexOf(p);
                    PointF ptf = mMap.grid().cellCenterPoint(pt);
                    // set the variable values and evaluate the expression
                    x_var = ptf.X;
                    y_var = ptf.Y;
                    if (expr.execute() != 0)
                    {
                        p = stand_id;
                    }
                }
            }
            else
            {
                // WRAP AROUND MODE
                // now loop over all cells ...
                double delta_x = GlobalSettings.instance().model().extent().Width;
                double delta_y = GlobalSettings.instance().model().extent().Height;
                for (int p = 0; p != mMap.grid().count(); ++p)
                {
                    Point pt = mMap.grid().indexOf(p);
                    PointF ptf = mMap.grid().cellCenterPoint(pt);
                    if (ptf.X < 0.0 || ptf.X > delta_x || ptf.Y < 0.0 || ptf.Y > delta_y)
                    {
                        continue;
                    }
                    // set the variable values and evaluate the expression
                    // we have to look at *9* positions to cover all wrap around cases....
                    for (int dx = -1; dx < 2; ++dx)
                    {
                        for (int dy = -1; dy < 2; ++dy)
                        {
                            x_var = ptf.X + dx * delta_x;
                            y_var = ptf.Y + dy * delta_y;
                            if (expr.execute() != 0)
                            {
                                p = stand_id;
                            }
                        }
                    }
                }
            }
            // after changing the map, recreate the index
            mMap.createIndex();
        }

        public double copyPolygonFromRect(MapGridWrapper source, int id_in, int id, double destx, double desty, double x1, double y1, double x2, double y2)
        {
            Grid<int> src = source.map().grid();
            Grid<int> dest = mMap.grid();
            Rectangle destRectangle = dest.rectangle();
            Rectangle r = new Rectangle(destRectangle.X, destRectangle.Y, destRectangle.Width, destRectangle.Height);
            Point rsize = dest.indexAt(new PointF((float)(destx + (x2 - x1)), (float)(desty + (y2 - y1))));
            r.Intersect(new Rectangle(dest.indexAt(new PointF((float)destx, (float)desty)), new Size(rsize)));
            Point dest_coord = dest.indexAt(new PointF((float)destx, (float)desty));
            Point offset = dest.indexAt(new PointF((float)x1, (float)y1));
            offset.X -= dest_coord.X;
            offset.Y -= dest_coord.Y;
            Debug.WriteLine("Rectangle " + r + " offset " + offset + " from " + new PointF((float)x1, (float)y1) + " to " + new PointF((float)destx, (float)desty));
            if (r == null)
            {
                return 0.0;
            }

            GridRunner<int> gr = new GridRunner<int>(dest, r);
            int i = 0, j = 0;
            for (gr.next(); gr.isValid(); gr.next())
            {
                //if (gr.current()>=0) {
                Point dp = gr.currentIndex();
                dp.X += offset.X;
                dp.Y += offset.Y;
                i++;
                if (src.isIndexValid(dp) && src.constValueAtIndex(dp) == id_in && gr.current() >= 0)
                {
                    gr.setCurrent(id);
                    //if (j<100) Debug.WriteLine(dp + gr.currentIndex() + src.constValueAtIndex(dp) + *gr.current();
                    ++j;
                }
                //}
            }
            //Debug.WriteLine("copyPolygonFromRect: copied" + j + "from" + i;

            // after changing the map, recreate the index
            // mMap.createIndex();
            return (double)j / 100.0; // in ha
        }

        public void createMapIndex()
        {
            if (mMap != null)
            {
                mMap.createIndex();
            }
        }

        public string name()
        {
            if (mMap != null)
            {
                return mMap.name();
            }
            else
            {
                return "invalid";
            }
        }

        public double area(int id)
        {
            if (mMap != null && mMap.isValid())
            {
                return mMap.area(id);
            }
            else
            {
                return -1;
            }
        }
    }
}
