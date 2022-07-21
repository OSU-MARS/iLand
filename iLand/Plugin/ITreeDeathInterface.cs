using iLand.Tree;

namespace iLand.Plugin
{
    internal interface ITreeDeathInterface
    {
        void OnTreeDeath(Trees tree, MortalityCause mortalityCause);
    }
}
