// C++/core/saplings.h
using System;

namespace iLand.Tree
{
    [Flags]
    public enum SaplingStatus : byte
    {
        None = 0x0,
        Sprout = 0x1,
        Browsed = 0x2
    }
}
