﻿using System;

namespace iLand.Input.Tree
{
    internal class TreeSizeRange
    {
        public UInt16 Age { get; set; }
        public float Count { get; set; }
        public float Density { get; set; }
        public float DbhFrom { get; set; }
        public float DbhTo { get; set; }
        public float HeightDiameterRatio { get; set; }
        public string TreeSpecies { get; set; }

        public TreeSizeRange(string treeSpecies)
        {
            TreeSpecies = treeSpecies;
        }
    }
}
