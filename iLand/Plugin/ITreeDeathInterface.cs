using iLand.Trees;

namespace iLand.Plugin
{
    internal interface ITreeDeathInterface
    {
        void TreeDeath(Tree tree, MortalityCause mortalityCause);
    }
}
