namespace iLand.Input.Tree
{
    internal class TreeFileByStandIDDataIndex
    {
        public int StandID { get; private init; }
        public int TreeFileName { get; private init; }

        public bool CanBeIndexFile { get; private init; }

        public TreeFileByStandIDDataIndex(CsvFile treeFile)
        {
            this.StandID = treeFile.GetColumnIndex("standID");
            this.TreeFileName = treeFile.GetColumnIndex("treeFileName");

            this.CanBeIndexFile = treeFile.Columns.Count == 2 && this.StandID >= 0 && this.TreeFileName >= 0;
        }
    }
}
