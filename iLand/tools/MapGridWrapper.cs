using iLand.Core;
using System;
using System.Diagnostics;
using System.Drawing;

namespace iLand.Tools
{
    public class MapGridWrapper
    {
        private bool mCreated;

        public MapGrid Map { get; private set; } ///< acccess for C++ classes

        public MapGridWrapper()
        {
            mCreated = false;
            if (GlobalSettings.Instance.Model == null)
            {
                return;
            }
            Map = GlobalSettings.Instance.Model.StandGrid;
        }

        public void Load(string file_name)
        {
            Map = new MapGrid(file_name);
            mCreated = true;
        }

        public bool IsValid()
        {
            return Map.IsValid();
        }

        public void Clear()
        {
            if (!mCreated)
            {
                // create a empty map
                Map = new MapGrid();
                Map.CreateEmptyGrid();
                mCreated = true;
            }
            Map.Grid.Initialize(0); // clear all data and set to 0
        }

        public void ClearProjectArea()
        {
            if (!mCreated)
            {
                // create a empty map
                Map = new MapGrid();
                Map.CreateEmptyGrid();
                mCreated = true;
            }
            MapGrid stand_grid = GlobalSettings.Instance.Model.StandGrid;
            if (stand_grid == null)
            {
                Debug.WriteLine("clearProjectArea: no valid stand grid to copy from!");
                return;
            }
            for (int index = 0; index < stand_grid.Grid.Count; ++index)
            {
                Map.Grid[index] = stand_grid.Grid[index] < 0 ? stand_grid.Grid[index] : 0;
            }
        }

        public void CreateStand(int stand_id, string paint_function, bool wrap_around)
        {
            if (Map == null)
            {
                throw new NotSupportedException("no valid map to paint on");
            }
            Expression expr = new Expression(paint_function);
            expr.AddVariable("x");
            expr.AddVariable("y");
            expr.CatchExceptions = true;
            if (!wrap_around)
            {
                // now loop over all cells ...
                for (int p = 0; p < Map.Grid.Count; ++p)
                {
                    Point pt = Map.Grid.IndexOf(p);
                    PointF ptf = Map.Grid.GetCellCenterPoint(pt);
                    // set the variable values and evaluate the expression
                    expr.SetVariable("x", ptf.X);
                    expr.SetVariable("y", ptf.Y);
                    if (expr.Execute() != 0)
                    {
                        p = stand_id;
                    }
                }
            }
            else
            {
                // WRAP AROUND MODE
                // now loop over all cells ...
                double delta_x = GlobalSettings.Instance.Model.WorldExtentUnbuffered.Width;
                double delta_y = GlobalSettings.Instance.Model.WorldExtentUnbuffered.Height;
                for (int p = 0; p != Map.Grid.Count; ++p)
                {
                    Point pt = Map.Grid.IndexOf(p);
                    PointF ptf = Map.Grid.GetCellCenterPoint(pt);
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
                            expr.SetVariable("x", ptf.X + dx * delta_x);
                            expr.SetVariable("y", ptf.Y + dy * delta_y);
                            if (expr.Execute() != 0)
                            {
                                p = stand_id;
                            }
                        }
                    }
                }
            }
            // after changing the map, recreate the index
            Map.CreateIndex();
        }

        public double CopyPolygonFromRect(MapGridWrapper source, int id_in, int id, double destx, double desty, double x1, double y1, double x2, double y2)
        {
            Grid<int> src = source.Map.Grid;
            Grid<int> dest = this.Map.Grid;
            Rectangle destRectangle = dest.CellExtent();
            Rectangle r = new Rectangle(destRectangle.X, destRectangle.Y, destRectangle.Width, destRectangle.Height);
            Point rsize = dest.IndexAt(new PointF((float)(destx + (x2 - x1)), (float)(desty + (y2 - y1))));
            r.Intersect(new Rectangle(dest.IndexAt(new PointF((float)destx, (float)desty)), new Size(rsize)));
            Point dest_coord = dest.IndexAt(new PointF((float)destx, (float)desty));
            Point offset = dest.IndexAt(new PointF((float)x1, (float)y1));
            offset.X -= dest_coord.X;
            offset.Y -= dest_coord.Y;
            Debug.WriteLine("Rectangle " + r + " offset " + offset + " from " + new PointF((float)x1, (float)y1) + " to " + new PointF((float)destx, (float)desty));
            if (r == null)
            {
                return 0.0;
            }

            GridRunner<int> gr = new GridRunner<int>(dest, r);
            int i = 0, j = 0;
            for (gr.MoveNext(); gr.IsValid(); gr.MoveNext())
            {
                //if (gr.current()>=0) {
                Point dp = gr.CurrentIndex();
                dp.X += offset.X;
                dp.Y += offset.Y;
                i++;
                if (src.Contains(dp) && src[dp] == id_in && gr.Current >= 0)
                {
                    gr.Current = id;
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

        public void CreateMapIndex()
        {
            if (Map != null)
            {
                Map.CreateIndex();
            }
        }

        public string Name()
        {
            if (Map != null)
            {
                return Map.Name;
            }
            else
            {
                return "invalid";
            }
        }

        public double Area(int id)
        {
            if (Map != null && Map.IsValid())
            {
                return Map.Area(id);
            }
            else
            {
                return -1;
            }
        }
    }
}
