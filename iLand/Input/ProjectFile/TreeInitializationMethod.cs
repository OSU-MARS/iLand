namespace iLand.Input.ProjectFile
{
    public enum TreeInitializationMethod
    {
        /// <summary>
        /// One tree file. Trees are copied to every resource unit as indicated in the tree file.
        /// </summary>
        /// <remarks>
        /// Single resource unit indicated by project.world.geometry.{height, width}.
        /// Tree file indicated by project.world.initialization.singleResourceUnitTreeFile.
        /// No saplings, though an empty sapling file may be specified.
        /// </remarks>
        SingleFile,
        /// <summary>
        /// One resource unit with one tree file. Trees are sampled from the tree file.
        /// </summary>
        /// <remarks>
        /// Single resource unit indicated by project.world.geometry.{height, width}.
        /// Tree file indicated by project.world.initialization.singleResourceUnitTreeFile.
        /// No saplings, though an empty sapling file may be specified.
        /// </remarks>
        SingleFileRandomized,
        /// <summary>
        /// One raster whose cells indicate stand IDs, one file listing tree files by stand ID, many files with trees, and one file with saplings.
        /// </summary>
        /// <remarks>
        /// Stand raster in project.world.standGrid.FileName.
        /// Stand ID to tree file mapping in project.world.initialization.treeFilesByStandID.
        /// Tree file names as listed in mapping file.
        /// Saplings file from project.world.initialization.saplingsByStandFile, which lists sapling species, counts, and size ranges by stand ID.
        /// </remarks>
        StandRaster
    }
}
