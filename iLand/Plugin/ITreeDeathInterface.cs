using iLand.Tree;

namespace iLand.Plugin
{
    internal interface ITreeDeathInterface
    {
        void TreeDeath(Trees tree, MortalityCause mortalityCause);
    }
}
