using iLand.Tree;

namespace iLand.Plugin
{
    internal interface ITreeDeathInterface
    {
        void OnTreeDeath(TreeListSpatial tree, MortalityCause mortalityCause);
    }
}
