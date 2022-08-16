using System;

namespace iLand.Input.ProjectFile
{
    [Flags]
    public enum ResourceUnitMemoryOutputs
    {
        None = 0x0,
        AllTreeSpeciesStatistics = 0x1,
        IndividualTrees = 0x2,
        IndividualTreeSpeciesStatistics = 0x4
    }
}
