namespace iLand.abe
{
    internal class SchedulerItem
    {
        public FMStand stand; ///< the stand to be harvested
        public double harvest; ///< the scheduled harvest in m3
        public double harvestPerHa; ///< harvest per ha
        public double scheduleScore; ///< the probability based on schedule timing
        public double harvestScore; ///< the probability of the activity
        public double score; ///< the total score of this ticked to be executed this year
        public HarvestType harvestType; ///< type of harvest
        public int enterYear; ///< the year the ticket was created
        public int optimalYear; ///< the (first) year where execution is considered as optimal
        public int scheduledYear; ///< planned execution year
        public int forbiddenTo; ///< year until which the harvest operation is forbidden
        public ActivityFlags flags; ///< the details of the activity/stand context

        public SchedulerItem()
        {
            stand = null;
            score = 0.0;
            scheduledYear = -1;
        }

        public static bool operator <(SchedulerItem item1, SchedulerItem item2)
        {
            // sort *descending*, i.e. after sorting, the item with the highest score is in front.
            //    if (this.score == item.score)
            //        return this.enterYear < item.enterYear; // higher prob. for items that entered earlier TODO: change to due/overdue
            if (item1.scheduledYear == item2.scheduledYear)
            {
                return item1.score > item2.score;
            }
            else
            {
                return item1.scheduledYear < item2.scheduledYear;
            }
        }

        public static bool operator >(SchedulerItem item1, SchedulerItem item2)
        {
            // sort *descending*, i.e. after sorting, the item with the highest score is in front.
            //    if (this.score == item.score)
            //        return this.enterYear < item.enterYear; // higher prob. for items that entered earlier TODO: change to due/overdue
            if (item1.scheduledYear == item2.scheduledYear)
            {
                return item1.score < item2.score;
            }
            else
            {
                return item1.scheduledYear > item2.scheduledYear;
            }
        }

        public void calculate()
        {
            if (flags.isExecuteImmediate())
            {
                score = 1.1; // above 1
            }
            else
            {
                score = scheduleScore * harvestScore;
            }
            if (score < 0.0)
            {
                score = 0.0;
            }
        }
    }
}
