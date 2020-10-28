using System;

namespace iLand.Tree
{
    /// (binary coded) tree flags
    [Flags]
    internal enum TreeFlags
    {
        Dead = 1,
        Debugging = 2,
        DeadFromBarkBeetles = 16,
        DeadFromWind = 32,
        DeadFromFire = 64,
        DeadCutAndDrop = 128,
        Harvested = 256,
        MarkedForCut = 512, // mark tree for being cut down
        MarkedForHarvest = 1024, // mark tree for being harvested
        CropTree = 2048, // mark as crop tree
        CropCompetitor = 4096 // mark as competitor for a crop tree
    }
}
