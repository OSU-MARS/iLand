using System;

namespace iLand.Output.Memory
{
    public class StandTrajectory : StandOrResourceUnitTrajectory
    {
        public int StandID { get; private init; }

        public StandTrajectory(int standID, int initialCapacityInYears)
            : base(initialCapacityInYears)
        {
            this.StandID = standID;
        }

        public void AddYear(StandTreeStatistics standTreeStatistics)
        {
            if (this.StandID != standTreeStatistics.StandID)
            {
                throw new ArgumentOutOfRangeException(nameof(standTreeStatistics));
            }
            base.AddYear(standTreeStatistics);
        }
    }
}
