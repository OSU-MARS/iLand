using System;

namespace iLand.World
{
    [Flags]
    public enum HeightCellFlags : byte
    {
        Default = 0x0,
        AdjacentToResourceUnit = 0x1,
        InResourceUnit = 0x2
    }
}
