using System.Drawing;

namespace iLand.core
{
    internal abstract class LayeredGrid<T> : LayeredGridBase
    {
        protected Grid<T> Grid { get; set; }

        public override RectangleF CellRect(Point p) { return Grid.GetCellRect(p); }
        public override RectangleF PhysicalSize() { return Grid.PhysicalSize; }
        public float CellSize() { return Grid.CellSize; }
        public override int SizeX() { return Grid.SizeX; }
        public override int SizeY() { return Grid.SizeY; }

        public LayeredGrid() 
        { 
            Grid = null; 
        }
        
        public LayeredGrid(Grid<T> grid) 
        { 
            Grid = grid; 
        }
    }
}
