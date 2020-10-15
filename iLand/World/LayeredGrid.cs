using System.Drawing;

namespace iLand.World
{
    internal abstract class LayeredGrid<T> : LayeredGridBase
    {
        protected Grid<T> Grid { get; set; }

        public override RectangleF CellRect(Point p) { return Grid.GetCellRect(p); }
        public override RectangleF PhysicalSize() { return Grid.PhysicalExtent; }
        public float CellSize() { return Grid.CellSize; }
        public override int SizeX() { return Grid.CellsX; }
        public override int SizeY() { return Grid.CellsY; }

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
