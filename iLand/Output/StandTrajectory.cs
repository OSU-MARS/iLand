using System;

namespace iLand.Output
{
    public class StandTrajectory : StandOrResourceUnitTrajectory
    {
        public int StandID { get; private init; }

        public StandTrajectory(int standID)
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
