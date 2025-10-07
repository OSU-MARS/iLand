using System;

namespace iLand.Tree
{
    // duplicate with TreeFlags but retained for now as allows packing from UInt16 to UInt8
    [Flags]
    public enum MortalityCause : byte
    { 
        None = 0x00,
        Stress = 0x01, 
        Harvest = 0x02,
        Disturbance = 0x04, // unspecified disturbance: could be fire, beetles, wind, other biotic disturbance, or something else
        BarkBeetles = 0x08,
        Fire = 0x10,
        Wind = 0x20,
        Salavaged = 0x40, // trees harvested after disturbance
        CutAndDrop = 0x80
    }
}
