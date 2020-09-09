using iLand.core;
using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    /** @class FMStand
        @ingroup abe
        The FMStand class encapsulates forest stands which are defined as polygons. FMStand tracks properties of the stands (e.g. mean volume), and
        is a central player in the ABE system.
        */
    internal class FMStand
    {
        private static Dictionary<FMStand, Dictionary<string, QJSValue>> mStandPropertyStorage = new Dictionary<FMStand, Dictionary<string, QJSValue>>();
        private int mId; ///< the unique numeric ID of the stand
        private FMUnit mUnit; ///< management unit that
        private FMSTP mSTP; ///< the stand treatment program assigned to this stand
        private Phase mPhase; ///< silvicultural phase
        private int mInitialId; ///< stand-id that was assigned in the beginning (this Id is kept when stands are split)
        private int mStandType; ///< enumeration of stand (compositional)
        private double mArea; ///< total stand area (ha)
        private double mTotalBasalArea; ///< basal area of the stand
        private double mAge; ///< average age (yrs) of the stand (basal area weighted)
        private double mVolume; ///< standing volume (m3/ha) of the stand
        private double mStems; ///< stems per ha (above 4m)
        private double mDbh; ///< mean dbh (basal area weighted, of trees>4m) in cm
        private double mHeight; ///< mean tree height (basal area weighted, of trees>4m), in m
        private double mTopHeight; ///< top height (mean height of the 100 thickest trees per ha)
        private double mScheduledHarvest; ///< harvest (m3) that is scheduled by activities
        private double mFinalHarvested; ///< m3 of timber volume that has been harvested (regeneration phase)
        private double mThinningHarvest; ///< m3 of timber that was harvested for thinning/tending
        private double mDisturbed; ///< removed due to disturbance

        private double mRemovedVolumeDecade; ///< removed volume of the decade (m3/ha)
        private double mRemovedVolumeTotal; ///< removed volume of the rotation (m3/ha)

        private double mLastMAIVolume; ///< safe the standing volume
        private double mMAIdecade; ///< decadal mean annual increment (m3/ha*yr)
        private double mMAItotal; ///< total (over the full rotation) mean annual increment (m3/ha*yr)

        private int mRotationStartYear; ///< absolute year the current rotation has started
        private int mYearsToWait; ///< variable indicates time to wait
        private int mCurrentIndex; ///< the index of the current activity
        private int mLastUpdate; ///< year of the last reload of data
        private int mLastExecution; ///< year of the last execution of an activity
        private int mLastExecutedIndex; ///< index of the last executed activity
        private int mLastRotationAge; ///< age at which the last rotation ended

        private double mU; ///< rotation length
        private int mSpeciesCompositionIndex; ///< index of the active target species composition
        private int mThinningIntensityClass; ///< currently active thinning intensity level

        // storage for stand meta data (species level)
        private List<SSpeciesStand> mSpeciesData;
        // storage for stand-specific management properties
        private List<ActivityFlags> mStandFlags;
        // additional property values for each stand
        private string mContextStr;

        /// set the stand to be managed by a given 'stp'
        public void setSTP(FMSTP stp) { mSTP = stp; }
        /// returns true if tracing is enabled for the stand
        public bool trace()
        {
            return property("trace").toBool();
        }
        public string context() { return mContextStr; }

        public void setArea(double new_area_ha) { mArea = new_area_ha; } // area in ha

        // general properties
        public int id() { return mId; }
        public FMUnit unit() { return mUnit; }
        public Phase phase() { return mPhase; }
        public int standType() { return mStandType; }
        public FMSTP stp() { return mSTP; }
        public int lastUpdate() { return mLastUpdate; }
        public int lastExecution() { return mLastExecution; }
        public int initialStandId() { return mInitialId; }
        public void setInitialId(int origin_id) { mInitialId = origin_id; }
        // agent properties
        /// rotation period (years)
        public double U() { return mU; }
        /// thinning intensity (class); 1: low, 2: medium, 3: high
        public int thinningIntensity() { return mThinningIntensityClass; }
        /// species composition key
        public int targetSpeciesIndex() { return mSpeciesCompositionIndex; }

        public void setU(double rotation_length) { mU = rotation_length; }
        public void setThinningIntensity(int th_class) { mThinningIntensityClass = th_class; }
        public void setTargetSpeciesIndex(int index) { mSpeciesCompositionIndex = index; }

        // stand properties
        /// total area of the stand (ha)
        public double area() { return mArea; }
        /// total basal area (m2/ha)
        public double basalArea() { return mTotalBasalArea; }
        /// (average) age of the stand (weighted with basal area)
        public double age() { return mAge; }
        /// total standing volume (m3/ha) in the stand
        public double volume() { return mVolume; }
        /// number of trees of the stand (stems/ha) (>4m)
        public double stems() { return mStems; }
        /// mean dbh (basal area weighted, of trees>4m) in cm
        public double dbh() { return mDbh; }
        /// mean tree height (basal area weighted, of trees>4m), in m
        public double height() { return mHeight; }
        /// top height (mean height of the 100 thickest trees/ha), in m
        public double topHeight() { return mTopHeight; }
        /// scheduled harvest (planned harvest by activities, m3)
        public double scheduledHarvest() { return mScheduledHarvest; }
        /// total realized harvest (m3 on the full stand area) (note: salvage harvest ist part of final harvest)
        public double totalHarvest() { return mFinalHarvested + mThinningHarvest; }
        /// total realized thinning/tending harvests (m3 on the full stand area)
        public double totalThinningHarvest() { return mThinningHarvest; }
        /// total disturbed timber volume, includes also disturbed trees *not* harvested, m3
        public double disturbedTimber() { return mDisturbed; }

        /// mean annual increment (MAI), m3 timber/ha for the last decade
        public double meanAnnualIncrement() { return mMAIdecade; }
        /// mean annual increment (MAI), m3 timber/ha for the full rotation period
        public double meanAnnualIncrementTotal() { return mMAItotal; }

        public bool readyForFinalHarvest() { return absoluteAge() > 0.8 * U(); } // { return currentActivity()?(currentFlags().isFinalHarvest() && currentFlags().isScheduled()):false; }

        public int nspecies() { return mSpeciesData.Count; }
        /// retrieve species-specific meta data by index (0: largest basal area share, up to nspecies()-1)
        public SSpeciesStand speciesData(int index) { return mSpeciesData[index]; }

        /// add a (simulated) harvest to the amount of planned harvest (used by the scheduling)
        public void addScheduledHarvest(double add_volume) { mScheduledHarvest += add_volume; }

        /// resets the harvest counters
        public void resetHarvestCounter() { mFinalHarvested = 0.0; mDisturbed = 0.0; mThinningHarvest = 0.0; }

        public int sleepYears() { return mYearsToWait; }

        // return stand-specific flags
        public ActivityFlags flags(int index) { return mStandFlags[index]; }
        /// flags of currently active Activity
        public ActivityFlags currentFlags() { return flags(mCurrentIndex); }
        /// get a pointer to the current activity; returns 0 if no activity is set.
        public Activity currentActivity() { return mCurrentIndex > -1 ? mStandFlags[mCurrentIndex].activity() : null; }

        /// get a pointer to the last executed activity; returns 0 if no activity has been executed before.
        public Activity lastExecutedActivity() { return mLastExecutedIndex > -1 ? mStandFlags[mLastExecutedIndex].activity() : null; }

        public int lastExecutionAge() { return absoluteAge() > 0 ? (int)absoluteAge() : mLastRotationAge; }

        // custom property storage
        public static void clearAllProperties() { mStandPropertyStorage.Clear(); }

        public FMStand(FMUnit unit, int id)
        {
            mUnit = unit;
            mId = id;
            mInitialId = id;
            mPhase = Phase.Invalid;

            // testing:
            mPhase = Phase.Tending;
            mStandType = 1; // just testing...

            mU = 0;
            mSpeciesCompositionIndex = -1;
            mThinningIntensityClass = -1;

            newRotatation();
            mSTP = null;
            mVolume = 0.0;
            mAge = 0.0;
            mTotalBasalArea = 0.0;
            mStems = 0.0;
            mDbh = 0.0;
            mHeight = 0.0;
            mScheduledHarvest = 0.0;
            mFinalHarvested = 0.0;
            mThinningHarvest = 0.0;
            mDisturbed = 0.0;
            mRotationStartYear = 0;
            mLastUpdate = -1;
            mLastExecution = -1;

            mCurrentIndex = -1;
            mLastExecutedIndex = -1;
            mLastRotationAge = -1;

            mArea = ForestManagementEngine.standGrid().area(mId) / Constant.cRUArea;
        }

        public void initialize()
        {
            if (mSTP == null)
            {
                throw new NotSupportedException(String.Format("initialize, no valid STP for stand {0}", id()));
            }
            // copy activity flags
            mStandFlags = mSTP.defaultFlags();
            mCurrentIndex = -1;
            mLastExecutedIndex = -1;
            mYearsToWait = 0;
            mContextStr = String.Format("S{1}Y{0}:", ForestManagementEngine.instance().currentYear(), id()); // initialize...

            // load data and aggregate averages
            reload();
            if (mRotationStartYear == 0.0) // only set if not explicitely set previously.
            {
                mRotationStartYear = ForestManagementEngine.instance().currentYear() - (int)age();
            }
            // when a stand is initialized, we assume that 20% of the standing volume
            // have been removed already.
            mRemovedVolumeTotal = volume() * 0.2;
            if (absoluteAge() > 0)
            {
                mMAItotal = volume() * 1.2 / absoluteAge();
            }
            else
            {
                mMAItotal = 0.0;
            }

            mMAIdecade = mMAItotal;
            mLastMAIVolume = volume();

            // find out the first activity...
            int min_years_to_wait = 100000;
            for (int i = 0; i < mStandFlags.Count; ++i)
            {
                // run the onSetup event
                // specifically set 'i' as the activity to be evaluated.
                FomeScript.setExecutionContext(this);
                FomeScript.bridge().activityObj().setActivityIndex(i);
                mStandFlags[i].activity().events().run("onSetup", null);

                if (!mStandFlags[i].enabled() || !mStandFlags[i].active())
                {
                    continue;
                }
                // set active to false which have already passed
                if (!mStandFlags[i].activity().isRepeatingActivity())
                {
                    if (!mStandFlags[i].activity().schedule().absolute && mStandFlags[i].activity().latestSchedule(U()) < age())
                    {
                        mStandFlags[i].setActive(false);
                    }
                    else
                    {
                        int delta = mStandFlags[i].activity().earlistSchedule(U()) - (int)age();
                        if (mStandFlags[i].activity().schedule().absolute)
                        {
                            delta += (int)age(); // absolute timing: starting from 0
                        }
                        if (delta < min_years_to_wait)
                        {
                            min_years_to_wait = Math.Max(delta, 0); // limit to 0 years
                            mCurrentIndex = i; // first activity to execute
                        }
                    }
                }
            }
            if (mCurrentIndex == -1)
            {
                // the stand is "outside" the time frames provided by the activities.
                // set the last activity with "force" = true as the active
                for (int i = mStandFlags.Count - 1; i >= 0; --i)
                {
                    if (mStandFlags[i].enabled() && mStandFlags[i].activity().schedule().force_execution == true)
                    {
                        mCurrentIndex = i;
                        break;
                    }
                }
            }

            if (min_years_to_wait < 100000)
            {
                sleep(min_years_to_wait);
            }
            // call onInit handler on the level of the STP
            mSTP.events().run("onInit", this);
            if (mCurrentIndex > -1)
            {
                mStandFlags[mCurrentIndex].activity().events().run("onEnter", this);

                // if it is a scheduled activity, then execute (to get initial estimates for harvests)
                if (currentFlags().isScheduled())
                {
                    executeActivity(currentActivity());
                }
            }
        }

        public void reset(FMSTP stp)
        {
            mSTP = stp;
            newRotatation();
            mCurrentIndex = -1;
        }

        public void checkArea()
        {
            mArea = ForestManagementEngine.standGrid().area(mId) / Constant.cRUArea;
        }

        public int relBasalAreaIsHigher(SSpeciesStand a, SSpeciesStand b)
        {
            if (a.relBasalArea < b.relBasalArea)
            {
                return -1;
            }
            if (a.relBasalArea > b.relBasalArea)
            {
                return 1;
            }
            return 0;
        }

        public void reload(bool force = false)
        {
            if (!force && mLastUpdate == ForestManagementEngine.instance().currentYear())
            {
                return;
            }

            using DebugTimer t = new DebugTimer("ABE:reload");
            // load all trees that are located on this stand
            mTotalBasalArea = 0.0;
            mVolume = 0.0;
            mAge = 0.0;
            mStems = 0.0;
            mDbh = 0.0;
            mHeight = 0.0;
            mTopHeight = 0.0;
            mLastUpdate = ForestManagementEngine.instance().currentYear();
            mSpeciesData.Clear();

            // load all trees of the forest stand (use the treelist of the current execution context)
            FMTreeList trees = ForestManagementEngine.instance().scriptBridge().treesObj();
            trees.setStand(this);
            trees.loadAll();

            //Debug.WriteLine("fmstand-reload: load trees from map:" + t.elapsed();
            // use: value_per_ha = value_stand * area_factor
            double area_factor = 1.0 / area();
            List<MutableTuple<Tree, double>> treelist = trees.trees();

            // calculate top-height: diameter of the 100 thickest trees per ha
            List<double> dbhvalues = new List<double>(trees.trees().Count);
            foreach (MutableTuple<Tree, double> it in treelist)
            {
                dbhvalues.Add(it.Item1.dbh());
            }

            double topheight_threshhold = 0.0;
            double topheight_height = 0.0;
            int topheight_trees = 0;
            if (treelist.Count > 0)
            {
                StatData s = new StatData(dbhvalues);
                topheight_threshhold = s.percentile((int)(100 * (1 - area() * 100 / treelist.Count))); // sorted ascending . thick trees at the end of the list
            }
            foreach (MutableTuple<Tree, double> it in treelist)
            {
                double ba = it.Item1.basalArea() * area_factor;
                mTotalBasalArea += ba;
                mVolume += it.Item1.volume() * area_factor;
                mAge += it.Item1.age() * ba;
                mDbh += it.Item1.dbh() * ba;
                mHeight += it.Item1.height() * ba;
                mStems++;
                SSpeciesStand sd = speciesData(it.Item1.species());
                sd.basalArea += ba;
                if (it.Item1.dbh() >= topheight_threshhold)
                {
                    topheight_height += it.Item1.height();
                    ++topheight_trees;
                }
            }
            if (mTotalBasalArea > 0.0)
            {
                mAge /= mTotalBasalArea;
                mDbh /= mTotalBasalArea;
                mHeight /= mTotalBasalArea;
                for (int i = 0; i < mSpeciesData.Count; ++i)
                {
                    mSpeciesData[i].relBasalArea = mSpeciesData[i].basalArea / mTotalBasalArea;
                }
            }
            if (topheight_trees > 0)
            {
                mTopHeight = topheight_height / (double)topheight_trees;
            }
            mStems *= area_factor; // convert to stems/ha
                                   // sort species data by relative share....
            mSpeciesData.Sort(relBasalAreaIsHigher);
        }

        public double absoluteAge()
        {
            return ForestManagementEngine.instance().currentYear() - mRotationStartYear;
        }

        public bool execute()
        {
            //  the age of the stand increases by one
            mAge++;

            // do nothing if we are still waiting (sleep)
            if (mYearsToWait > 0)
            {
                if (--mYearsToWait > 0)
                {
                    return false;
                }
            }
            mContextStr = String.Format("S{1}Y{0}:", ForestManagementEngine.instance().currentYear(), id());

            // what to do if there is no active activity??
            if (mCurrentIndex == -1)
            {
                if (trace())
                {
                    Debug.WriteLine(context() + " *** No action - no currently active activity ***");
                }
                return false;
            }
            if (trace())
            {
                Debug.WriteLine(context() + " *** start evaulate activity:" + currentActivity().name());
            }
            // do nothing if there is already an activity in the scheduler
            if (currentFlags().isPending())
            {
                if (trace())
                {
                    Debug.WriteLine(context() + " *** No action - stand in the scheduler. ***");
                }
                return false;
            }

            // do nothing if the the current year is not within the window of opportunity of the activity
            double p_schedule = currentActivity().scheduleProbability(this);
            if (p_schedule == -1.0)
            {
                if (trace())
                {
                    Debug.WriteLine(context() + " *** Activity expired. ***");
                }
                // cancel the activity
                currentFlags().setActive(false);
                afterExecution(true);
                return false;
            }
            if (p_schedule >= 0.0 && p_schedule < 0.00001)
            {
                if (trace())
                {
                    Debug.WriteLine(context() + " *** No action - Schedule probability 0.0 ***");
                }
                return false;
            }

            // we need to renew the stand data
            reload();

            // check if there are some constraints that prevent execution....
            double p_execute = currentActivity().execeuteProbability(this);
            if (p_execute == 0.0)
            {
                if (trace())
                {
                    Debug.WriteLine(context() + " *** No action - Constraints preventing execution. ***");
                }
                return false;
            }

            // ok, we should execute the current activity.
            // if it is not scheduled, it is executed immediately, otherwise a ticket is created.
            if (currentFlags().isScheduled())
            {
                // ok, we schedule the current activity
                if (trace())
                {
                    Debug.WriteLine(context() + " adding ticket for execution.");
                }

                mScheduledHarvest = 0.0;
                bool should_schedule = currentActivity().evaluate(this);
                if (trace())
                {
                    Debug.WriteLine(context() + " evaluated stand. add a ticket: " + should_schedule);
                }
                if (should_schedule)
                {
                    mUnit.scheduler().addTicket(this, currentFlags(), p_schedule, p_execute);
                }
                else
                {
                    // cancel the activity
                    currentFlags().setActive(false);
                    afterExecution(true);
                }
                return should_schedule;
            }
            else
            {
                // execute immediately
                if (trace())
                {
                    Debug.WriteLine(context() + " executing activty " + currentActivity().name());
                }
                mScheduledHarvest = 0.0;
                bool executed = currentActivity().execute(this);
                if (currentActivity() == null) // special case: the activity invalidated the active activtity
                {
                    return executed;
                }
                if (!currentActivity().isRepeatingActivity())
                {
                    currentFlags().setActive(false);
                    afterExecution(!executed); // check what comes next for the stand
                }
                return executed;
            }
        }

        public bool executeActivity(Activity act)
        {
            int old_activity_index = mCurrentIndex;

            int new_index = stp().activityIndex(act);
            bool result = false;
            if (new_index > -1)
            {
                mCurrentIndex = new_index;
                int old_years = mYearsToWait;
                mYearsToWait = 0;
                result = execute();
                mAge--; // undo modification of age
                mYearsToWait = old_years; // undo...
            }
            mCurrentIndex = old_activity_index;
            return result;
        }

        public bool afterExecution(bool cancel)
        {
            // check if an agent update is necessary
            unit().agent().type().agentUpdateForStand(this, currentFlags().activity().name(), -1);

            // is called after an activity has run
            int tmin = 10000000;
            int indexmin = -1;
            for (int i = 0; i < mStandFlags.Count; ++i)
            {
                if (mStandFlags[i].isForcedNext())
                {
                    mStandFlags[i].setForceNext(false); // reset flag
                    indexmin = i;
                    break; // we "jump" to this activity
                }
            }

            if (indexmin == -1)
            {
                // check if a restart is needed
                // TODO: find a better way!!
                if (currentFlags().isFinalHarvest())
                {
                    // we have reached the last activity
                    for (int i = 0; i < mStandFlags.Count; ++i)
                    {
                        mStandFlags[i].setActive(true);
                    }
                    newRotatation();
                    reload();
                }

                // look for the next (enabled) activity.
                for (int i = 0; i < mStandFlags.Count; ++i)
                {
                    if (mStandFlags[i].enabled() && mStandFlags[i].active() && !mStandFlags[i].isRepeating())
                    {
                        if (mStandFlags[i].activity().earlistSchedule() < tmin)
                        {
                            tmin = mStandFlags[i].activity().earlistSchedule();
                            indexmin = i;
                        }
                    }
                }
            }

            if (!cancel)
            {
                currentActivity().events().run("onExecuted", this);
            }
            else
            {
                currentActivity().events().run("onCancel", this);
            }

            if (indexmin != mCurrentIndex)
            {
                // call events:
                currentActivity().events().run("onExit", this);
                if (indexmin > -1 && indexmin < mStandFlags.Count)
                {
                    mStandFlags[indexmin].activity().events().run("onEnter", this);
                }
            }
            mLastExecutedIndex = mCurrentIndex;

            mCurrentIndex = indexmin;
            if (mCurrentIndex > -1)
            {
                int to_sleep = tmin - (int)absoluteAge();
                if (to_sleep > 0)
                {
                    sleep(to_sleep);
                }
            }
            mScheduledHarvest = 0.0; // reset

            mLastExecution = ForestManagementEngine.instance().currentYear();
            return mCurrentIndex > -1;
        }

        public void notifyTreeRemoval(Tree tree, int reason)
        {
            double removed_volume = tree.volume();
            mVolume -= removed_volume / area();

            // for MAI calculations: store removal regardless of the reason
            mRemovedVolumeDecade += removed_volume / area();
            mRemovedVolumeTotal += removed_volume / area();

            TreeRemovalType r = (TreeRemovalType)reason;
            if (r == TreeRemovalType.TreeDeath)
            {
                return; // do nothing atm
            }
            else if (r == TreeRemovalType.TreeHarvest)
            {
                // regular harvest
                if (currentActivity() != null)
                {
                    if (currentFlags().isFinalHarvest())
                    {
                        mFinalHarvested += removed_volume;
                    }
                    else
                    {
                        mThinningHarvest += removed_volume;
                    }
                }
            }
            else if (r == TreeRemovalType.TreeDisturbance)
            {
                // if we have an active salvage activity, then store
                mDisturbed += removed_volume;
                // check if we have an (active) salvage activity; both the activity flags and the stand flags need to be "enabled"
                if (mSTP.salvageActivity() != null && mSTP.salvageActivity().standFlags().enabled() && mSTP.salvageActivity().standFlags(this).enabled())
                {
                    if (mSTP.salvageActivity().evaluateRemove(tree))
                    {
                        mFinalHarvested += removed_volume;
                        tree.setIsHarvested(); // set the flag that the tree is removed from the forest
                    }
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public bool notifyBarkBeetleAttack(double generations, int infested_px_per_ha)
        {
            // check if we have an (active) salvage activity; both the activity flags and the stand flags need to be "enabled"
            if (mSTP.salvageActivity() != null && mSTP.salvageActivity().standFlags().enabled() && mSTP.salvageActivity().standFlags(this).enabled())
            {
                return mSTP.salvageActivity().barkbeetleAttack(this, generations, infested_px_per_ha);
            }
            return false;
        }

        public void sleep(int years_to_sleep)
        {
            mYearsToWait = Math.Max(mYearsToWait, Math.Max(years_to_sleep, 0));
        }

        public double calculateMAI()
        {
            // MAI: delta standing volume + removed volume, per year
            // removed volume: mortality, management, disturbances
            mMAIdecade = ((mVolume - mLastMAIVolume) + mRemovedVolumeDecade) / 10.0;
            if (absoluteAge() > 0)
            {
                mMAItotal = (mVolume + mRemovedVolumeTotal) / absoluteAge();
            }
            mLastMAIVolume = mVolume;
            // reset counters
            mRemovedVolumeDecade = 0.0;
            return meanAnnualIncrementTotal();
        }

        public double basalArea(string species_id)
        {
            foreach (SSpeciesStand sd in mSpeciesData)
            {
                if (sd.species.id() == species_id)
                {
                    return sd.basalArea;
                }
            }
            return 0.0;
        }

        public double relBasalArea(string species_id)
        {
            foreach (SSpeciesStand sd in mSpeciesData)
            {
                if (sd.species.id() == species_id)
                {
                    return sd.relBasalArea;
                }
            }
            return 0.0;
        }

        public void setAbsoluteAge(double age)
        {
            mRotationStartYear = ForestManagementEngine.instance().currentYear() - (int)age;
            mAge = age;
        }

        public void setProperty(string name, QJSValue value)
        {
            // save a property value for the current stand
            mStandPropertyStorage[this][name] = value;
        }

        public QJSValue property(string name)
        {
            // check if values are already stored for the current stand
            if (!mStandPropertyStorage.ContainsKey(this))
            {
                return null;
            }
            // check if something is stored for the property name (return a undefined value if not)
            if (!mStandPropertyStorage[this].ContainsKey(name))
            {
                return null;
            }
            return mStandPropertyStorage[this][name];
        }

        public List<string> info()
        {
            List<string> lines = new List<string>();
            lines.Add(String.Format("id: {0}", id()));
            lines.Add(String.Format("unit: {0}", unit().id()));
            lines.Add("-");
            lines.AddRange(unit().info());
            lines.Add("/-"); // sub sections
            if (currentActivity() != null)
            {
                lines.Add(String.Format("activity: {0}", currentActivity().name()));
                lines.Add("-");
                lines.AddRange(currentActivity().info());
                // activity properties
                lines.Add(String.Format("active: {0}", currentFlags().active()));
                lines.Add(String.Format("enabled: {0}", currentFlags().enabled()));
                lines.Add(String.Format("simulate: {0}", currentFlags().isDoSimulate()));
                lines.Add(String.Format("execute immediate: {0}", currentFlags().isExecuteImmediate()));
                lines.Add(String.Format("final harvest: {0}", currentFlags().isFinalHarvest()));
                lines.Add(String.Format("use scheduler: {0}", currentFlags().isScheduled()));
                lines.Add(String.Format("in scheduler: {0}", currentFlags().isPending()));
                lines.Add("/-");
            }
            lines.Add(String.Format("agent: {0}", unit().agent().type().name()));
            lines.Add(String.Format("STP: {0}", stp() != null ? stp().name() : "-"));
            lines.Add(String.Format("U (yrs): {0}", U()));
            lines.Add(String.Format("thinning int.: {0}", thinningIntensity()));
            lines.Add(String.Format("last update: {0}", lastUpdate()));
            lines.Add(String.Format("sleep (years): {0}", sleepYears()));
            lines.Add(String.Format("scheduled harvest: {0}", scheduledHarvest()));
            lines.Add(String.Format("basal area: {0}", basalArea()));
            lines.Add(String.Format("volume: {0}", volume()));
            lines.Add(String.Format("age: {0}", age()));
            lines.Add(String.Format("absolute age: {0}", absoluteAge()));
            lines.Add(String.Format("N/ha: {0}", stems()));
            lines.Add(String.Format("MAI (decadal) m3/ha*yr: {0}", meanAnnualIncrement()));
            lines.Add("Basal area per species");
            for (int i = 0; i < nspecies(); ++i)
            {
                lines.Add(String.Format("{0}: {1}", speciesData(i).species.id(), speciesData(i).basalArea));
            }

            lines.Add("All activities");
            lines.Add("-");
            foreach (ActivityFlags a in mStandFlags)
            {
                lines.Add(String.Format("{0} (active): {1}", a.activity().name(), a.active()));
                lines.Add(String.Format("{0} (enabled): {1}", a.activity().name(), a.enabled()));
            }
            lines.Add("/-");

            // stand properties
            if (mStandPropertyStorage.ContainsKey(this))
            {
                Dictionary<string, QJSValue> props = mStandPropertyStorage[this];
                lines.Add(String.Format("properties: {0}", props.Count));
                lines.Add("-");
                foreach (KeyValuePair<string, QJSValue> i in props)
                {
                    lines.Add(String.Format("{0}: {1}", i.Key, i.Value));
                }
                lines.Add("/-");
            }

            // scheduler info
            lines.AddRange(unit().constScheduler().info(id()));

            return lines;
        }

        private void newRotatation()
        {
            mLastRotationAge = (int)absoluteAge();
            mRotationStartYear = ForestManagementEngine.instance().currentYear(); // reset stand age to 0.
            mRemovedVolumeTotal = 0.0;
            mRemovedVolumeDecade = 0.0;
            mLastMAIVolume = 0.0;
            mMAIdecade = 0.0;
            mMAItotal = 0.0;
            // use default values
            setU(unit().U());
            setThinningIntensity(unit().thinningIntensity());
            unit().agent().type().agentUpdateForStand(this, null, 0); // update at age 0? maybe switch to new STP?
        }

        public SSpeciesStand speciesData(Species species)
        {
            for (int i = 0; i < mSpeciesData.Count; ++i)
            {
                if (mSpeciesData[i].species == species)
                {
                    return mSpeciesData[i];
                }
            }

            SSpeciesStand stand = new SSpeciesStand();
            mSpeciesData.Add(stand);
            return stand;
        }
    }
}
