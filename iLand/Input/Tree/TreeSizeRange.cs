using iLand.Tree;
using System;

namespace iLand.Input.Tree
{
    internal class TreeSizeRange
    {
        public UInt16 Age { get; set; }
        public int TreesPerResourceUnit { get; set; }
        public float Density { get; set; }
        public float DbhFrom { get; set; }
        public float DbhTo { get; set; }
        public float HeightDiameterRatio { get; set; }
        public WorldFloraID SpeciesID { get; set; }

        public TreeSizeRange(WorldFloraID treeSpeciesID)
        {
            SpeciesID = treeSpeciesID;
        }
    }
}
