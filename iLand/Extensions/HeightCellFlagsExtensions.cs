using iLand.World;

namespace iLand.Extensions
{
    public static class HeightCellFlagsExtensions
    {
        public static bool IsAdjacentToResourceUnit(this HeightCellFlags flags)
        {
            return flags.HasFlag(HeightCellFlags.AdjacentToResourceUnit);
        }

        public static bool IsInResourceUnit(this HeightCellFlags flags)
        {
            return flags.HasFlag(HeightCellFlags.InResourceUnit);
        }
    }
}
