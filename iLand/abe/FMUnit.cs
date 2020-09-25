using iLand.output;
using iLand.tools;
using Microsoft.Collections.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace iLand.abe
{
    /** @class FMUnit
        @ingroup abe
        The FMUnit class encapsulates a forest management unit, comprised of forest stands. Units are the base level at which
        the scheduling works.
        */
    internal class FMUnit
    {
        private string mId;
        private readonly Agent mAgent;
        private readonly Scheduler mScheduler;
        private int mNumberOfStands; ///< the number of stands
        public double mAnnualHarvestTarget; ///< planned annual harvest (final harvests) (m3)
        private double mAnnualThinningTarget; ///< planned annual harvests (thinnings and tendings) (m3)
        private double mRealizedHarvest; ///< sum of realized harvest in the current planning period (final harvests) (m3)
        private double mRealizedHarvestLastYear; ///< the sum of harvests up to the last year (final harvests) (m3)
        public double mMAI; ///< mean annual increment (m3/ha)
        private double mHDZ; ///< mean "haubarer" annual increment (m3/ha)
        public double mMeanAge; ///< mean age of the planning unit
        private double mTotalArea; ///< total area of the unit (ha)
        public double mTotalVolume; ///< total standing volume (m3)
        public double mTotalPlanDeviation; ///< cumulative deviation from the planned harvest (m3/ha)

        private double mU; ///< rotation length
        private int mSpeciesCompositionIndex; ///< index of the active target species composition
        private int mThinningIntensityClass; ///< currently active thinning intensity level
        private string mHarvestMode; ///< type of applicable harvesting technique (e.g. skidder, cablecrane)

        private double mAverageMAI; ///< reference value for mean annual increment

        public string id() { return mId; }
        public void setId(string id) { mId = id; }
        public Scheduler scheduler() { return mScheduler; }
        public Scheduler constScheduler() { return mScheduler; }
        public Agent agent() { return mAgent; }
        public double area() { return mTotalArea; } ///< total area of the unit (ha)
        public int numberOfStands() { return mNumberOfStands; } ///< the total number of stands
        public void setNumberOfStands(int new_number) { mNumberOfStands = new_number; } ///< set the number of stands
        public double volume() { return mTotalVolume / area(); } ///< total volume of the unit (m3/ha)
        public double annualIncrement() { return mMAI; } ///< mean annual increment (m3/ha)
        // agent properties
        /// rotation period (years)
        public double U() { return mU; }
        /// thinning intensity (class); 1: low, 2: medium, 3: high
        public int thinningIntensity() { return mThinningIntensityClass; }
        /// species composition key
        public int targetSpeciesIndex() { return mSpeciesCompositionIndex; }
        public string harvestMode() { return mHarvestMode; }

        public void setU(double rotation_length) { mU = rotation_length; }
        public void setThinningIntensity(int th_class) { mThinningIntensityClass = th_class; }
        public void setTargetSpeciesCompositionIndex(int index) { mSpeciesCompositionIndex = index; }
        public void setHarvestMode(string new_mode) { mHarvestMode = new_mode; }

        public void setAverageMAI(double avg_mai) { mAverageMAI = avg_mai; }
        public double averageMAI() { return mAverageMAI; }

        public FMUnit(Agent agent)
        {
            mAgent = agent;
            mScheduler = null;
            mAnnualHarvestTarget = -1.0;
            mRealizedHarvest = 0.0;
            mMAI = 0.0; mHDZ = 0.0; mMeanAge = 0.0;
            mTotalArea = 0.0; mTotalPlanDeviation = 0.0;
            mTotalVolume = 0.0;
            mNumberOfStands = 0;
            mU = 100;
            mThinningIntensityClass = 2;
            mSpeciesCompositionIndex = 0;
            mAverageMAI = 0.0;

            //if (agent.type().schedulerOptions().useScheduler)
            // explicit scheduler only for stands/units that include more than one stand
            mScheduler = new Scheduler(this);
        }

        /// record realized harvests on the unit (all harvests)
        public void AddRealizedHarvest(double harvest_m3) { mRealizedHarvest += harvest_m3; }

        public double GetAnnualTotalHarvest() { return mRealizedHarvest - mRealizedHarvestLastYear; } ///< total m3 produced in final harvests in this year

        public void Aggregate()
        {
            // loop over all stands
            // collect some data....
            double age = 0.0;
            double volume = 0.0;
            double totalarea = 0.0;
            MultiValueDictionary<FMUnit, FMStand> stands = ForestManagementEngine.instance().stands();
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> it in stands)
            {
                if (it.Key != this)
                {
                    continue;
                }
                foreach (FMStand s in it.Value)
                {
                    age += s.age() * s.area();
                    volume += s.volume() * s.area();
                    totalarea += s.area();
                }
            }
            if (totalarea > 0.0)
            {
                age /= totalarea;
                volume /= totalarea;
            }
            Debug.WriteLine("unit " + id() + " volume (m3/ha) " + volume + " age " + age + " planned harvest: todo");
        }
        
        public List<string> Info()
        {
            return new List<string>() { String.Format("(accumulated) harvest: {0}", mRealizedHarvest),
                                        String.Format("MAI: {0}", mMAI),
                                        String.Format("HDZ: {0}", mHDZ),
                                        String.Format("average age: {0}", mMeanAge),
                                        String.Format("decadal plan: {0}", mAnnualHarvestTarget),
                                        String.Format("current plan: {0}", constScheduler() != null ? constScheduler().harvestTarget() : 0.0) };
        }

        public double AnnualThinningHarvest()
        {
            MultiValueDictionary<FMUnit, FMStand> stands = ForestManagementEngine.instance().stands();
            double harvested = 0.0;
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> it in stands)
            {
                if (it.Key != this)
                {
                    continue;
                }
                foreach (FMStand stand in it.Value)
                {
                    harvested += stand.totalThinningHarvest();
                }
            }
            return harvested;
        }

        public void ResetHarvestCounter()
        {
            if (scheduler() != null)
            {
                scheduler().ResetHarvestCounter();
            }
        }

        public void ManagementPlanUpdate()
        {
            double period_length = 10.0;
            // calculate the planned harvest in the next planning period (i.e., 10yrs).
            // this is the sum of planned operations that are already in the scheduler.
            mScheduler.GetPlannedHarvests(out double plan_final, out double plan_thinning);
            // the actual harvests of the last planning period
            //double realized = mRealizedHarvest;

            mRealizedHarvest = 0.0; // reset
            mRealizedHarvestLastYear = 0.0;

            // preparations:
            // MAI-calculation for all stands:
            double total_area = 0.0;
            double age = 0.0;
            double mai = 0.0;
            double hdz = 0.0;
            double volume = 0.0;
            MultiValueDictionary<FMUnit, FMStand> stands = ForestManagementEngine.instance().stands();
            foreach (KeyValuePair<FMUnit, IReadOnlyCollection<FMStand>> it in stands)
            {
                foreach (FMStand stand in it.Value)
                {
                    stand.Reload();
                    stand.CalculateMeanAnnualIncrement();
                    // calculate sustainable total harvest (following Breymann)
                    double area = stand.area();
                    mai += stand.meanAnnualIncrementTotal() * area; // m3/yr
                    age += stand.AbsoluteAge() * area;
                    volume += stand.volume() * area;
                    // HDZ: "haubarer" average increment: timber that is ready for final harvest
                    if (stand.IsReadyForFinalHarvest())
                    {
                        hdz += stand.volume() / stand.AbsoluteAge() * area; //(0.1* stand.U()) * area; // note: changed!!!! was: volume/age * area
                    }
                    total_area += area;
                }
            }
            // reset
            ForestManagementEngine.instance().scriptBridge().treesObj().SetStand(null);
            mTotalArea = total_area;
            if (total_area == 0.0)
            {
                return;
            }

            mai /= total_area; // m3/ha*yr area weighted average of annual increment
            age /= total_area; // area weighted mean age
            hdz /= total_area; // =sum(Vol/age * share)

            mMAI = mai;
            //mMAI = mMAI * 1.15; // 15% increase, hack WR
            mHDZ = hdz;
            mMeanAge = age;
            mTotalVolume = volume;

            double rotation_length = U();
            double h_tot = mai * 2.0 * age / rotation_length;
            // double h_reg = hdz * 2.0 * age / rotation_length;
            double h_reg = h_tot * 0.85; // hack!
            h_reg *= agent().schedulerOptions().harvestIntensity;
            h_tot *= agent().schedulerOptions().harvestIntensity;
            double h_thi = Math.Max(h_tot - h_reg, 0.0);

            Debug.WriteLine("plan-update for unit " + id() + ": h-tot: " + h_tot + " h_reg: " + h_reg + " h_thi: " + h_thi + " of total volume: " + volume);
            double sf = mAgent.UseSustainableHarvest();
            // we do not calculate sustainable harvest levels.
            // do a pure bottom up calculation
            double bottom_up_harvest = (plan_final / period_length) / total_area; // m3/ha*yr

            // the sustainable harvest yield is the current yield and some carry over from the last period
            double sustainable_harvest = h_reg;
            //    if (mAnnualHarvestTarget>0.0) {
            //        double delta = realized/(total_area*period_length) - mAnnualHarvestTarget;
            //        // if delta > 0: timber removal was too high -> plan less for the current period, and vice versa.
            //        sustainable_harvest -= delta;
            //    }
            mAnnualHarvestTarget = sustainable_harvest * sf + bottom_up_harvest * (1.0 - sf);
            mAnnualHarvestTarget = Math.Max(mAnnualHarvestTarget, 0.0);

            mAnnualThinningTarget = (plan_thinning / period_length) / total_area; // m3/ha*yr

            if (scheduler() != null)
            {
                scheduler().SetHarvestTargets(mAnnualHarvestTarget, mAnnualThinningTarget);
            }
        }

        public void RunAgent()
        {
            // we need to set an execution context
            // BUGBUG: what about other stands in this management unit?
            FMStand stand = ForestManagementEngine.instance().stands()[this].First();
            if (stand == null)
            {
                throw new NotSupportedException("Invalid stand in runAgent");
            }

            // avoid parallel execution of agent-code....
            lock (FomeScript.bridge())
            {
                FomeScript.SetExecutionContext(stand, true); // true: add also agent as 'agent'

                QJSValue val;
                QJSValue agent_type = agent().type().jsObject();
                if (agent_type.Property("run").IsCallable())
                {
                    val = agent_type.Property("run").CallWithInstance(agent_type);
                    Debug.WriteLine("running agent-function 'run' for unit " + id() + ": " + val.ToString());
                }
                else
                {
                    Debug.WriteLine("function 'run' is not a valid function of agent-type " + agent().type().name());
                }
            }
        }

        public void UpdatePlanOfCurrentYear()
        {
            if (scheduler() != null)
            {
                return;
            }
            if (mTotalArea == 0.0)
            {
                throw new NotSupportedException("FMUnit:updatePlan: unit area = 0???");
            }
            // compare the harvests of the last year to the plan:
            double harvests = mRealizedHarvest - mRealizedHarvestLastYear;
            mRealizedHarvestLastYear = mRealizedHarvest;

            // difference in m3/ha
            double delta = harvests / mTotalArea - mAnnualHarvestTarget;
            mTotalPlanDeviation += delta;

            // apply decay function for deviation
            mTotalPlanDeviation *= mAgent.schedulerOptions().deviationDecayRate;

            // relative deviation: >0: too many harvests
            double rel_deviation = mAnnualHarvestTarget != 0.0 ? mTotalPlanDeviation / mAnnualHarvestTarget : 0;

            // the current deviation is reduced to 50% in rebounce_yrs years.
            double rebounce_yrs = mAgent.schedulerOptions().scheduleRebounceDuration;

            double new_harvest = mAnnualHarvestTarget * (1.0 - rel_deviation / rebounce_yrs);

            // limit to minimum/maximum parameter
            new_harvest = Math.Max(new_harvest, mAgent.schedulerOptions().minScheduleHarvest);
            new_harvest = Math.Min(new_harvest, mAgent.schedulerOptions().maxScheduleHarvest);
            scheduler().SetHarvestTargets(new_harvest, mAnnualThinningTarget);
        }
    }
}
