using System.Drawing;

namespace iLand.core
{
    internal abstract class LayeredGrid<T> : LayeredGridBase
    {
        protected Grid<T> mGrid;

        public override RectangleF cellRect(Point p) { return mGrid.cellRect(p); }
        public override RectangleF metricRect() { return mGrid.metricRect(); }
        public float cellsize() { return mGrid.cellsize(); }
        public override int sizeX() { return mGrid.sizeX(); }
        public override int sizeY() { return mGrid.sizeY(); }

        // unused in C++
        //public virtual double value(T ptr, int index) { return value(mGrid.constValueAtIndex(mGrid.indexOf(ptr)), index); }
        //public override double value(int grid_index, int index) { return value(mGrid.constValueAtIndex(grid_index), index); }
        //public override double value(float x, float y, int index) { return value(mGrid.constValueAt(x, y), index); }
        //public override double value(PointF world_coord, int index) { return mGrid.coordValid(world_coord) ? value(mGrid.constValueAt(world_coord), index) : 0.0; }
        //public override double value(int ix, int iy, int index) { return value(mGrid.constValueAtIndex(ix, iy), index); }

        public LayeredGrid() { mGrid = null; }
        public LayeredGrid(Grid<T> grid) { mGrid = grid; }

        // unused in C++
        //public override void range(ref double rMin, ref double rMax, int index)
        //{
        //    rMin = 9999999999.0;
        //    rMax = -99999999999.0;
        //    for (int i = 0; i < mGrid.count(); ++i)
        //    {
        //        rMin = Math.Min(rMin, value(i, index));
        //        rMax = Math.Max(rMax, value(i, index));
        //    }
        //}

        // unused in C++
        /// extract a (newly created) grid filled with the value of the variable given by 'index'
        /// caller need to free memory!
        //public Grid<double> copyGrid(int index)
        //{
        //    Grid<double> data_grid = new Grid<double>(mGrid.metricRect(), mGrid.cellsize());
        //    for (int i = 0; i < mGrid.count(); ++i)
        //    {
        //        mGrid[i] = value(i, index);
        //    }
        //    return data_grid;
        //}

        // unused in C++
        //public static string gridToESRIRaster(LayeredGrid<T> grid, string name)
        //{
        //    int index = grid.indexOf(name);
        //    if (index < 0)
        //    {
        //        return null;
        //    }
        //    Vector3D model = new Vector3D(grid.metricRect().Left, grid.metricRect().Top, 0.0);
        //    Vector3D world = new Vector3D();
        //    GisGrid.modelToWorld(model, world);
        //    string result = String.Format("ncols {0}\r\nnrows {1}{6}xllcorner {2}{6}yllcorner {3}{6}cellsize {4}{6}NODATA_value {5}{6}",
        //                            grid.sizeX(), grid.sizeY(), world.x(), world.y(), grid.cellsize(), -9999, System.Environment.NewLine);

        //    StringBuilder res = new StringBuilder();
        //    char sep = ' ';
        //    for (int y = grid.sizeY() - 1; y >= 0; --y)
        //    {
        //        for (int x = 0; x < grid.sizeX(); x++)
        //        {
        //            res.Append(grid.value(x, y, index) + sep);
        //        }
        //        res.Append(System.Environment.NewLine);
        //    }
        //    return result + res;
        //}
    }
}
