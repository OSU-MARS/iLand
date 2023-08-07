using System;

namespace iLand.Output.Memory
{
    public class StandTrajectory : StandOrResourceUnitTrajectory
    {
        public UInt32 StandID { get; private init; }

        public StandTrajectory(UInt32 standID, int initialCapacityInYears)
            : base(initialCapacityInYears)
        {
            this.StandID = standID;
        }

        public void AddYear(StandLiveTreeAndSaplingStatistics standTreeStatistics)
        {
            if (this.StandID != standTreeStatistics.StandID)
            {
                throw new ArgumentOutOfRangeException(nameof(standTreeStatistics));
            }
            base.AddYear(standTreeStatistics);
        }
    }
}
