using System;

namespace iLand.Tree
{
    /// (binary coded) tree flags
    [Flags]
    public enum TreeFlags : UInt16
    {
        None = 0x0000,
        Dead = 0x0001,
        Debugging = 0x0002,
        DeadFromBarkBeetles = 0x0010,
        DeadFromWind = 0x0020,
        DeadFromFire = 0x0040,
        DeadCutAndDrop = 0x0080,
        Harvested = 0x0100,
        MarkedForCut = 0x0200, // mark tree for being cut down
        MarkedForHarvest = 0x0400, // mark tree for being harvested
        CropTree = 0x1000, // crop tree
        CropCompetitor = 0x2000, // competitor to a crop tree
        BioticDisturbance = 0x4000 // affected or killed by biotic disturbance (e.g. BITE module)
    }
}
