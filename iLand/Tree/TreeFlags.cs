// C++/core/tree.h
using System;

namespace iLand.Tree
{
    /// (binary coded) tree flags
    [Flags]
    public enum TreeFlags : UInt16 // C++ Tree::Flags
    {
        None = 0x0000,
        BioticDisturbance = 0x0001, // affected or killed by biotic disturbance (e.g. BITE module)
        Dead = 0x0002,
        DeadFromBarkBeetles = 0x0004,
        DeadFromWind = 0x0008,
        DeadFromFire = 0x0010,
        DeadFromCutAndDrop = 0x0020,
        DeadFromHarvest = 0x0040,
        Debugging = 0x0080,
        MarkedAsReserve = 0x0100, // tree is a reserve tree, C++ noHarvest (marked as a habitat tree, and can be easily spared for management)
        MarkedForCutAndDrop = 0x0200, // mark tree for being cut down
        MarkedForHarvest = 0x0400, // mark tree for being harvested
        CropTree = 0x0800, // crop tree
        CropCompetitor = 0x1000 // competitor to a crop tree
    }
}
