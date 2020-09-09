using iLand.tools;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace iLand.abe
{
    /** @class Scheduler
        @ingroup abe
        The Scheduler class implements the logic of scheduling the when and what of activties.
        */
    internal class Scheduler
    {
        private List<SchedulerItem> mItems; ///< the list of active tickets
        private MultiValueDictionary<int, SchedulerItem> mSchedule;
        private FMUnit mUnit;
        public double mExtraHarvest; ///< extra harvests due to disturbances m3
        public double mFinalCutTarget; ///< current harvest target for regeneration harvests (m3/ha)
        private double mThinningTarget; ///< current harvest target for thinning/tending operations (m3/ha)

        private const int MAX_YEARS = 20;

        public Scheduler(FMUnit unit)
        {
            mUnit = unit;
            mExtraHarvest = 0.0;
            mFinalCutTarget = 0.0;
        }

        /// at the end of the year, reset the salvage harvests
        public void resetHarvestCounter() { mExtraHarvest = 0.0; }
        /// set the harvest target for the unit (m3/ha) for the current year.
        /// target_m3_ha: the
        public void setHarvestTarget(double target_m3_ha, double thinning_target_m3_ha)
        {
            mFinalCutTarget = Math.Max(target_m3_ha, 0.01);
            mThinningTarget = Math.Max(thinning_target_m3_ha, 0.01);
        }
        public double harvestTarget() { return mFinalCutTarget; }

        /// add an planned activity for a given stand.
        /// @param stand the stand to add
        /// @param flags the execution flags (activty x stand)
        /// @param prob_schedule the probability from the activity-scheduling algorithm at the time of adding the ticket
        /// @param prob_execute the probability for executing the activity (based on the constraints of the activity)
        public void addTicket(FMStand stand, ActivityFlags flags, double prob_schedule, double prob_execute)
        {
            if (FMSTP.verbose())
            {
                Debug.WriteLine("ticked added for stand " + stand.id());
            }
            flags.setIsPending(true);
            SchedulerItem item = new SchedulerItem();
            item.stand = stand;
            item.flags = flags;
            item.enterYear = ForestManagementEngine.instance().currentYear();
            item.optimalYear = item.enterYear + flags.activity().optimalSchedule(stand.U()) - (int)stand.absoluteAge();
            item.scheduledYear = item.optimalYear;
            // estimate growth from now to the optimal time - we assume that growth of the last decade continues
            int t = item.optimalYear - item.enterYear; // in t years harvest is optimal
            double time_factor = 0.0;
            if (stand.volume() > 0.0)
                time_factor = t * stand.meanAnnualIncrement() / stand.volume();
            item.harvest = stand.scheduledHarvest() * (1.0 + time_factor);
            item.harvestPerHa = item.harvest / stand.area();
            item.harvestType = flags.isFinalHarvest() ? HarvestType.EndHarvest : HarvestType.Thinning;
            item.scheduleScore = prob_schedule;
            item.harvestScore = prob_execute;
            item.forbiddenTo = 0;
            item.calculate(); // set score
            mItems.Add(item);
        }

        public void run()
        {
            // update the plan if necessary...
            if (FMSTP.verbose() && mItems.Count > 0)
            {
                Debug.WriteLine("running scheduler for unit " + mUnit.id() + ". # of active items: " + mItems.Count);
            }

            double harvest_in_queue = 0.0;
            double total_final_harvested = mExtraHarvest;
            double total_thinning_harvested = 0.0;
            //mExtraHarvest = 0.0;
            if (FMSTP.verbose() && total_final_harvested > 0.0)
            {
                Debug.WriteLine("Got extra harvest (e.g. salvages), m3=" + total_final_harvested);
            }

            int current_year = ForestManagementEngine.instance().currentYear();

            // update the schedule probabilities....
            for (int it = 0; it < mItems.Count; ++it)
            {
                SchedulerItem item = mItems[it];
                double p_sched = item.flags.activity().scheduleProbability(item.stand);
                item.scheduleScore = p_sched;
                item.calculate();
                if (item.stand.trace())
                {
                    Debug.WriteLine(item.stand.context() + " scheduler scores (harvest schedule total): " + item.harvestScore + item.scheduleScore + item.score);
                }

                // drop item if no schedule to happen any more
                if (item.score == 0.0)
                {
                    if (item.stand.trace())
                    {
                        Debug.WriteLine(item.stand.context() + " dropped activity " + item.flags.activity().name() + " from scheduler.");
                    }
                    item.flags.setIsPending(false);
                    item.flags.setActive(false);

                    item.stand.afterExecution(true); // execution canceled
                    mItems.RemoveAt(it--);
                }
                else
                {
                    // handle item
                    harvest_in_queue += item.harvest;
                }
            }

            if (mUnit.agent().schedulerOptions().useScheduler)
            {
                updateCurrentPlan();
            }

            // sort the probabilities, highest probs go first....
            //qSort(mItems);
            //qSort(mItems.begin(), mItems.end(), )
            mItems.Sort(ItemComparator);
            if (FMSTP.verbose())
            {
                dump();
            }

            int no_executed = 0;
            double harvest_scheduled = 0.0;
            // now execute the activities with the highest ranking...

            for (int it = 0; it < mItems.Count; ++it)
            {
                SchedulerItem item = mItems[it];
                // ignore stands that are currently banned (only for final harvests)
                if (item.forbiddenTo > current_year && item.flags.isFinalHarvest())
                {
                    continue;
                }

                if (item.scheduledYear > current_year)
                {
                    break; // finished! TODO: check if this works ok ;)
                }

                bool remove = false;
                bool final_harvest = item.flags.isFinalHarvest();
                //
                double rel_harvest;
                if (final_harvest)
                {
                    rel_harvest = total_final_harvested / mUnit.area() / mFinalCutTarget;
                }
                else
                {
                    rel_harvest = total_thinning_harvested / mUnit.area() / mThinningTarget;
                }

                double min_exec_probability = 0; // calculateMinProbability( rel_harvest );
                rel_harvest = (total_final_harvested + total_thinning_harvested) / mUnit.area() / (mFinalCutTarget + mThinningTarget);
                if (rel_harvest > mUnit.agent().schedulerOptions().maxHarvestLevel)
                {
                    break;
                }

                if (rel_harvest + item.harvest / mUnit.area() / (mFinalCutTarget + mThinningTarget) > mUnit.agent().schedulerOptions().maxHarvestLevel)
                {
                    // including the *current* harvest, the threshold would be exceeded . draw a random number
                    if (RandomGenerator.drandom() < 0.5)
                    {
                        break;
                    }
                }

                if (item.score >= min_exec_probability)
                {
                    // execute activity:
                    if (item.stand.trace())
                    {
                        Debug.WriteLine(item.stand.context() + " execute activity " + item.flags.activity().name() + " score " + item.score + " planned harvest: " + item.harvest);
                    }
                    harvest_scheduled += item.harvest;

                    bool executed = item.flags.activity().execute(item.stand);
                    if (final_harvest)
                    {
                        total_final_harvested += item.stand.totalHarvest();
                    }
                    else
                    {
                        total_thinning_harvested += item.stand.totalHarvest();
                    }

                    item.flags.setIsPending(false);
                    if (!item.flags.activity().isRepeatingActivity())
                    {
                        item.flags.setActive(false);
                        item.stand.afterExecution(!executed); // check what comes next for the stand
                    }
                    no_executed++;

                    // flag neighbors of the stand, if a clearcut happened
                    // this is to avoid large unforested areas
                    if (executed && final_harvest)
                    {
                        if (FMSTP.verbose())
                        {
                            Debug.WriteLine(item.stand.context() + " ran final harvest. flag neighbors");
                        }
                        // simple rule: do not allow harvests for neighboring stands for 7 years
                        item.forbiddenTo = current_year + 7;
                        List<int> neighbors = ForestManagementEngine.standGrid().neighborsOf(item.stand.id());
                        foreach (SchedulerItem nit in mItems)
                        {
                            if (neighbors.Contains(nit.stand.id()))
                            {
                                nit.forbiddenTo = current_year + 7;
                            }
                        }
                    }

                    remove = true;
                }
                if (remove)
                {
                    // removing item from scheduler
                    if (item.stand.trace())
                    {
                        Debug.WriteLine(item.stand.context() + " removing activity " + item.flags.activity().name() + "from scheduler.");
                    }
                    mItems.RemoveAt(it--);
                }
            }
            if (FMSTP.verbose() && no_executed > 0)
            {
                Debug.WriteLine("scheduler finished for " + mUnit.id() + ". # of items executed (n/volume): " + no_executed + " (" + harvest_scheduled + " m3), total: " + mItems.Count + "(" + harvest_in_queue + "m3)");
            }
        }

        private int ItemComparator(SchedulerItem lx, SchedulerItem rx)
        {
            if (lx.scheduledYear == rx.scheduledYear)
            {
                if (lx.score < rx.score)
                {
                    return -1;
                }
                if (lx.score > rx.score)
                {
                    return 1;
                }
                return 0;
            }
            else
            {
                if (lx.scheduledYear < rx.scheduledYear)
                {
                    return -1;
                }
                if (lx.scheduledYear > rx.scheduledYear)
                {
                    return -1;
                }
                return 0;
            }
        }

        public bool forceHarvest(FMStand stand, int max_years)
        {
            // check if we have the stand in the list:
            foreach (SchedulerItem nit in mItems)
            {
                SchedulerItem item = nit;
                if (item.stand == stand)
                {
                    if (Math.Abs(item.optimalYear - GlobalSettings.instance().currentYear()) < max_years)
                    {
                        item.flags.setExecuteImmediate(true);
                        return true;
                    }
                }
            }
            return false;
        }

        public void addExtraHarvest(FMStand stand, double volume, HarvestType type)
        {
            // Q_UNUSED(stand); Q_UNUSED(type); // at least for now
            mExtraHarvest += volume;
        }

        public double plannedHarvests(out double rFinal, out double rThinning)
        {
            rFinal = 0.0; 
            rThinning = 0.0;
            int current_year = ForestManagementEngine.instance().currentYear();
            foreach (SchedulerItem nit in mItems)
            {
                if (nit.optimalYear < current_year + 10)
                {
                    if (nit.flags.isFinalHarvest())
                    {
                        rFinal += nit.harvest; // scheduled harvest in m3
                    }
                    else
                    {
                        rThinning += nit.harvest;
                    }
                }
            }
            return rFinal + rThinning;
        }

        public double scoreOf(int stand_id)
        {
            // lookup stand in scheduler list
            SchedulerItem item = null;
            foreach (SchedulerItem nit in mItems)
            {
                if (nit.stand.id() == stand_id)
                {
                    item = nit;
                    break;
                }
            }
            if (item == null)
            {
                return -1;
            }
            return item.score;
        }

        public List<string> info(int stand_id)
        {
            List<string> lines = new List<string>();
            SchedulerItem si = item(stand_id);
            if (si != null)
            {
                lines.Add("-");
                lines.Add(String.Format("type: {0}", si.harvestType == HarvestType.Thinning ? "Thinning" : "End harvest"));
                lines.Add(String.Format("schedule score: {0}", si.scheduleScore));
                lines.Add(String.Format("total score: {0}", si.score));
                lines.Add(String.Format("scheduled vol/ha: {0}", si.harvestPerHa));
                lines.Add(String.Format("postponed to year: {0}", si.forbiddenTo));
                lines.Add(String.Format("in scheduler since: {0}", si.enterYear));
                lines.Add("/-");
            }
            return lines;
        }

        private void updateCurrentPlan()
        {
            if (mItems.Count == 0)
            {
                return;
            }
            double[] scheduled_harvest = new double[MAX_YEARS];
            double[] state = new double[MAX_YEARS];

            for (int i = 0; i < MAX_YEARS; ++i)
            {
                scheduled_harvest[i] = 0.0;
                state[i] = 0.0;
            }

            scheduled_harvest[0] = mExtraHarvest; // salvaging
            mSchedule.Clear();
            int current_year = ForestManagementEngine.instance().currentYear();
            int max_year = 0;
            double total_plan = mExtraHarvest;
            foreach (SchedulerItem item in mItems)
            {
                mSchedule.Add(Math.Max(item.optimalYear, current_year), item);
                total_plan += item.harvest;
                int year_index = Math.Min(Math.Max(0, item.optimalYear - current_year), MAX_YEARS - 1);
                scheduled_harvest[year_index] += item.harvest;
                max_year = Math.Max(max_year, year_index);
            }

            double mean_harvest = total_plan / (max_year + 1.0);
            double level = (mFinalCutTarget + mThinningTarget) * mUnit.area();

            level = Math.Max(level, mean_harvest);

            for (int i = 0; i < MAX_YEARS; ++i)
            {
                state[i] = scheduled_harvest[i] > level ? 1.0 : 0.0;
            }
            int max_iter = mItems.Count * 10;
            bool updated = false;
            do
            {
                updated = false;
                do
                {
                    // look for a relocate candidate and relocate

                    // look for the highest planned harvest
                    int year = -1; double max_harvest = -1.0;
                    for (int i = 0; i < MAX_YEARS; ++i)
                    {
                        if (scheduled_harvest[i] > max_harvest && state[i] == 1.0)
                        {
                            year = i;
                            max_harvest = scheduled_harvest[i];
                        }
                    }
                    // if no further slot is found, then stop
                    if (year == -1)
                    {
                        break;
                    }
                    // if the maximum harvest in the next x years is below the current plan,
                    // then we simply call it a day (and execute everything on its "optimal" point in time)
                    if (max_harvest < level)
                    {
                        break;
                    }
                    state[year] = -1.0; // processed
                                        // pick an element of that year and try to find another year
                    int pick = RandomGenerator.irandom(0, mSchedule[year + current_year].Count);
                    SchedulerItem item = mSchedule[year + current_year].ToList()[pick];
                    if (item == null)
                    {
                        Debug.WriteLine("updateCurrentPlan(): no item found for year" + year + ", #elements:" + mSchedule[year + current_year].Count);
                        break;
                    }

                    // try to change something only if the years' schedule is above the level without the focal item
                    if (scheduled_harvest[year] - item.harvest > level)
                    {
                        int calendar_year = year + current_year;
                        int dist = -1;
                        do
                        {
                            double value = item.flags.activity().scheduleProbability(item.stand, calendar_year + dist);
                            if (value > 0.0 && year + dist >= 0 && year + dist < MAX_YEARS)
                            {
                                if (state[year + dist] == 0.0)
                                {
                                    // simple: finish!
                                    mSchedule.Remove(year + current_year, item);
                                    scheduled_harvest[year] -= item.harvest;
                                    scheduled_harvest[year + dist] += item.harvest;
                                    mSchedule.Add(calendar_year + dist, item);
                                    updated = true;
                                    // reset also the processed flag
                                    state[year] = scheduled_harvest[year] > level ? 1.0 : 0.0;
                                    state[year + dist] = scheduled_harvest[year + dist] > level ? 1.0 : 0.0;
                                    break;
                                }
                            }
                            // series of: -1 +1 -2 +2 -3 +3 ...
                            if (dist < 0)
                            {
                                dist = -dist; // switch sign
                            }
                            else
                            {
                                dist = -(dist + 1); // switch sign and add 1
                            }
                        } while (dist < MAX_YEARS);
                        if (updated)
                        {
                            break;
                        }
                    } // end if
                    if (--max_iter < 0)
                    {
                        Debug.WriteLine("scheduler: max iterations reached in updateCurrentPlan()");
                        break;
                    }
                } 
                while (1 == 1); // continue until no further candidate exists or a relocate happened
            } 
            while (updated); // stop when no new candidate is found

            // write back the execution plan....
            foreach (KeyValuePair<int, IReadOnlyCollection<SchedulerItem>> it in mSchedule)
            {
                foreach (SchedulerItem item in it.Value)
                {
                    item.scheduledYear = it.Key;
                }
            }
            if (FMSTP.verbose())
            {
                dump();
            }
        }

        public void dump()
        {
            if (mItems.Count == 0)
            {
                return;
            }
            Debug.WriteLine("***** Scheduler items **** Unit: " + mUnit.id());
            Debug.WriteLine("stand.id, scheduled.year, score, opt.year, act.name, planned.harvest");
            foreach (SchedulerItem it in mItems)
            {
                SchedulerItem item = it;
                Debug.WriteLine(String.Format("{0}, {1}, {2}, {3}, {4}, {5}", item.stand.id(), item.scheduledYear, item.score, item.optimalYear,
                                                                              item.flags.activity().name(), item.harvest));
            }
        }

        /// find scheduler item for 'stand_id' or return NULL.
        public SchedulerItem item(int stand_id)
        {
            foreach (SchedulerItem nit in mItems)
            {
                if (nit.stand.id() == stand_id)
                {
                    return nit;
                }
            }
            return null;
        }
    }
}
