// C++/core/saplings.h: SaplingCell::ECellState
namespace iLand.Tree
{
    public enum SaplingCellState : byte
    {
        NotOnLandscape = 0, // not stockable (outside project area)
        Empty = 1, // the cell has no slots occupied (no saplings on the cell)
        Grass = 2, // empty and has grass cover (see grass module)
        Free = 3, // seedlings may establish on the cell (at least one slot occupied)
        Full = 4 // cell is full (no establishment) (either all slots used or one slot > 1.3m)
    }
}
