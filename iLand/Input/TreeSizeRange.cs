namespace iLand.Input
{
    internal class TreeSizeRange
    {
        public int Age { get; set; }
        public float Count { get; set; }
        public float Density { get; set; }
        public float DbhFrom { get; set; }
        public float DbhTo { get; set; }
        public float HeightDiameterRatio { get; set; }
        public string TreeSpecies { get; set; }

        public TreeSizeRange(string treeSpecies)
        {
            this.TreeSpecies = treeSpecies;
        }
    }
}
