﻿using iLand.Simulation;

namespace iLand.World
{
    public class MapGridWrapper
    {
        //private bool mCreated;

        public MapGrid StandGrid { get; private init; }

        public MapGridWrapper(Model model)
        {
            //this.mCreated = false;
            if (model == null)
            {
                return;
            }
            this.StandGrid = model.StandGrid;
        }

        //public void Load(Model model, string fileName)
        //{
        //    this.StandGrid = new MapGrid(model, fileName);
        //    this.mCreated = true;
        //}

        //public bool IsValid()
        //{
        //    return StandGrid.IsValid();
        //}

        //public void Clear(Model model)
        //{
        //    if (!mCreated)
        //    {
        //        // create a empty map
        //        StandGrid = new MapGrid();
        //        StandGrid.CreateEmptyGrid(model);
        //        mCreated = true;
        //    }
        //    StandGrid.Grid.Fill(0); // clear all data and set to 0
        //}

        //public void ClearProjectArea(Model model)
        //{
        //    if (!mCreated)
        //    {
        //        // create a empty map
        //        StandGrid = new MapGrid();
        //        StandGrid.CreateEmptyGrid(model);
        //        mCreated = true;
        //    }
        //    MapGrid standGrid = model.StandGrid;
        //    if (standGrid == null)
        //    {
        //        Debug.WriteLine("clearProjectArea: no valid stand grid to copy from!");
        //        return;
        //    }
        //    for (int index = 0; index < standGrid.Grid.Count; ++index)
        //    {
        //        StandGrid.Grid[index] = standGrid.Grid[index] < 0 ? standGrid.Grid[index] : 0;
        //    }
        //}

        //public void CreateStand(Model model, int standID, string paintFunction, bool wrapAround)
        //{
        //    if (StandGrid == null)
        //    {
        //        throw new NotSupportedException("no valid map to paint on");
        //    }
        //    Expression expr = new Expression(paintFunction);
        //    expr.AddVariable("x");
        //    expr.AddVariable("y");
        //    expr.CatchExceptions = true;
        //    if (!wrapAround)
        //    {
        //        // now loop over all cells ...
        //        for (int p = 0; p < StandGrid.Grid.Count; ++p)
        //        {
        //            Point pt = StandGrid.Grid.GetCellPosition(p);
        //            PointF ptf = StandGrid.Grid.GetCellCenterPosition(pt);
        //            // set the variable values and evaluate the expression
        //            expr.SetVariable("x", ptf.X);
        //            expr.SetVariable("y", ptf.Y);
        //            if (expr.Execute(model) != 0)
        //            {
        //                p = standID;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        // WRAP AROUND MODE
        //        // now loop over all cells ...
        //        double delta_x = model.WorldExtentUnbuffered.Width;
        //        double delta_y = model.WorldExtentUnbuffered.Height;
        //        for (int p = 0; p != StandGrid.Grid.Count; ++p)
        //        {
        //            Point pt = StandGrid.Grid.GetCellPosition(p);
        //            PointF ptf = StandGrid.Grid.GetCellCenterPosition(pt);
        //            if (ptf.X < 0.0 || ptf.X > delta_x || ptf.Y < 0.0 || ptf.Y > delta_y)
        //            {
        //                continue;
        //            }
        //            // set the variable values and evaluate the expression
        //            // we have to look at *9* positions to cover all wrap around cases....
        //            for (int dx = -1; dx < 2; ++dx)
        //            {
        //                for (int dy = -1; dy < 2; ++dy)
        //                {
        //                    expr.SetVariable("x", ptf.X + dx * delta_x);
        //                    expr.SetVariable("y", ptf.Y + dy * delta_y);
        //                    if (expr.Execute(model) != 0)
        //                    {
        //                        p = standID;
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    // after changing the map, recreate the index
        //    StandGrid.CreateIndex(model);
        //}

        //public double CopyPolygonFromRect(MapGridWrapper source, int id_in, int id, double destx, double desty, double x1, double y1, double x2, double y2)
        //{
        //    Grid<int> src = source.StandGrid.Grid;
        //    Grid<int> dest = this.StandGrid.Grid;
        //    Rectangle destRectangle = dest.GetCellExtent();
        //    Rectangle r = new Rectangle(destRectangle.X, destRectangle.Y, destRectangle.Width, destRectangle.Height);
        //    Point rsize = dest.GetCellIndex(new PointF((destx + (x2 - x1)), (desty + (y2 - y1))));
        //    Point destination = dest.GetCellIndex(destx, desty);
        //    r.Intersect(new Rectangle(destination.X, destination.Y, rsize.X, rsize.Y));
        //    Point dest_coord = dest.GetCellIndex(new PointF(destx, desty));
        //    Point offset = dest.GetCellIndex(new PointF(x1, y1));
        //    offset.X -= dest_coord.X;
        //    offset.Y -= dest_coord.Y;
        //    Debug.WriteLine("Rectangle " + r + " offset " + offset + " from " + new PointF(x1, y1) + " to " + new PointF(destx, desty));
        //    if (r == null)
        //    {
        //        return 0.0;
        //    }

        //    GridWindowEnumerator<int> destinationRunner = new GridWindowEnumerator<int>(dest, r);
        //    int i = 0, j = 0;
        //    while (destinationRunner.MoveNext())
        //    {
        //        //if (gr.current()>=0) {
        //        Point dp = destinationRunner.GetCellPosition();
        //        dp.X += offset.X;
        //        dp.Y += offset.Y;
        //        i++;
        //        if (src.Contains(dp) && src[dp] == id_in && destinationRunner.Current >= 0)
        //        {
        //            destinationRunner.Current = id;
        //            //if (j<100) Debug.WriteLine(dp + gr.currentIndex() + src.constValueAtIndex(dp) + *gr.current();
        //            ++j;
        //        }
        //        //}
        //    }
        //    //Debug.WriteLine("copyPolygonFromRect: copied" + j + "from" + i;

        //    // after changing the map, recreate the index
        //    // mMap.createIndex();
        //    return (double)j / 100.0; // in ha
        //}

        //public void CreateMapIndex(Model model)
        //{
        //    if (StandGrid != null)
        //    {
        //        StandGrid.CreateIndex(model);
        //    }
        //}

        //public string Name()
        //{
        //    if (StandGrid != null)
        //    {
        //        return StandGrid.Name;
        //    }
        //    else
        //    {
        //        return "invalid";
        //    }
        //}

        //public double Area(int id)
        //{
        //    if (StandGrid != null && StandGrid.IsValid())
        //    {
        //        return StandGrid.GetArea(id);
        //    }
        //    else
        //    {
        //        return -1;
        //    }
        //}
    }
}
