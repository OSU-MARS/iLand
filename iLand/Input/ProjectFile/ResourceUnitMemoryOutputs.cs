using System;

namespace iLand.Input.ProjectFile
{
    [Flags]
    public enum ResourceUnitMemoryOutputs
    {
        None = 0x0,
        AllTreeSpeciesStatistics = 0x1,
        IndividualTreeSpeciesStatistics = 0x2
    }
}
