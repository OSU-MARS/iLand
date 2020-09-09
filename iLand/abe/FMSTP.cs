using iLand.tools;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.abe
{
    /** @class FMSTP
        @ingroup abe
        The FMSTP class encapsulates a stand treatment program, which is defined in Javascript.
        */
    internal class FMSTP
    {
        private static bool mVerbose = false; ///< debug mode

        private string mName; ///< the name of the stand treatment program
        private Events mEvents;
        private bool mHasRepeatingActivities; ///< true, if the STP contains repeating activities
        private List<Activity> mActivities; ///< container for the activities of the STP
        private List<ActivityFlags> mActivityStand; ///< base data for stand-specific STP info.
        private List<string> mActivityNames;  ///< names of all available activities
        // special activities
        private ActSalvage mSalvage;

        // STP-level properties
        private int[] mRotationLength; ///< three levels (low, medium,high) of rotation length

        private QJSValue mOptions; ///< user-defined options of the STP

        public string name() { return mName; }
        public int activityIndex(Activity act) { return mActivities.IndexOf(act); }

        /// defaultFlags() is used to initalized the flags for indiv. forest stands
        public List<ActivityFlags> defaultFlags() { return mActivityStand; }
        public Events events() { return mEvents; }
        public QJSValue JSoptions() { return mOptions; }

        /// rotation length (years)
        public int rotationLengthOfType(int type) { if (type > 0 && type < 4) return mRotationLength[type - 1]; return 0; }
        public int rotationLengthType(int length) { for (int i = 0; i < 3; ++i) if (mRotationLength[i] == length) return i + 1; return -1; } // TODO: fix
        public ActSalvage salvageActivity() { return mSalvage; }

        /// if verbose is true, detailed debug information is provided.
        public static void setVerbose(bool verbose) { mVerbose = verbose; }
        public static bool verbose() { return mVerbose; } ///< returns true in debug mode

        public FMSTP()
        {
            mRotationLength = new int[3];
            mSalvage = null;
            mRotationLength[0] = 90; // sensible defaults
            mRotationLength[1] = 100;
            mRotationLength[2] = 110;
            mOptions = new QJSValue(0);
        }

        public Activity activity(string name)
        {
            int idx = mActivityNames.IndexOf(name);
            if (idx == -1)
            {
                return null;
            }
            return mActivities[idx];
        }

        public int activityScheduledEarlier(Activity a, Activity b)
        {
            if (a.earlistSchedule() < b.earlistSchedule())
            {
                return -1;
            }
            if (a.earlistSchedule() > b.earlistSchedule())
            {
                return 1;
            }
            return 0;
        }

        public void setup(QJSValue js_value, string name)
        {
            clear();

            if (String.IsNullOrEmpty(name) == false)
            {
                mName = name;
            }

            // (1) scan recursively the data structure and create
            //     all activites
            internalSetup(js_value, 0);

            // (2) create all other required meta information (such as ActivityStand)
            // sort activites based on the minimum execution time
            mActivities.Sort(activityScheduledEarlier);

            mActivityNames.Clear();
            mHasRepeatingActivities = false;
            for (int i = 0; i < mActivities.Count; ++i)
            {
                mActivityNames.Add(mActivities[i].name());
                mActivityStand.Add(mActivities[i].standFlags()); // stand = null: create a copy of the activities' base flags
                mActivities[i].setIndex(i);
                if (mActivities[i].isRepeatingActivity())
                {
                    mHasRepeatingActivities = true;
                }
                if (mActivities[i].standFlags().isSalvage())
                {
                    mSalvage = (ActSalvage)mActivities[i];
                    mHasRepeatingActivities = false;
                }
            }

            // (3) set up top-level events
            mEvents.setup(js_value, new List<string>() { "onInit", "onExit" });
        }

        public bool executeRepeatingActivities(FMStand stand)
        {
            if (mSalvage != null)
            {
                if (stand.totalHarvest() != 0.0 || stand.property("_run_salvage").toBool())
                {
                    // at this point totalHarvest is only disturbance related harvests.
                    stand.executeActivity(mSalvage);
                }
            }
            if (!mHasRepeatingActivities)
            {
                return false;
            }

            bool result = false;
            for (int i = 0; i < mActivities.Count; ++i)
            {
                if (mActivities[i].schedule().repeat)
                {
                    if (!stand.flags(i).active() || !stand.flags(i).enabled())
                    {
                        continue;
                    }
                    if (stand.trace())
                    {
                        Debug.WriteLine("running repeating activity " + mActivities[i].name());
                    }
                    result |= stand.executeActivity(mActivities[i]);
                }
            }

            return result; // return true if at least one repeating activity was executed.
        }

        public void evaluateDynamicExpressions(FMStand stand)
        {
            foreach (Activity act in mActivities)
        {
                act.evaluateDyanamicExpressions(stand);
            }
        }

        // read the setting from the setup-javascript object
        private void internalSetup(QJSValue js_value, int level)
        {
            // top-level
            if (js_value.hasOwnProperty("schedule"))
            {
                setupActivity(js_value, "unnamed");
                return;
            }

            // nested objects
            if (js_value.isObject())
            {
                QJSValueIterator it = new QJSValueIterator(js_value);
                while (it.hasNext())
                {
                    it.next();
                    // parse special properties
                    if (it.name() == "U" && it.value().isArray())
                    {
                        List<int> list = (List<int>)it.value().toVariant();
                        if (list.Count != 3)
                        {
                            throw new NotSupportedException("STP: the 'U'-property needs to be an array with three elements!");
                        }
                        for (int i = 0; i < list.Count; ++i)
                        {
                            mRotationLength[i] = list[i];
                        }
                        continue;
                    }
                    if (it.name() == "options")
                    {
                        mOptions = it.value();
                        continue;
                    }
                    if (it.value().hasOwnProperty("type"))
                    {
                        // try to set up as activity
                        setupActivity(it.value(), it.name());
                    }
                    else if (it.value().isObject() && !it.value().isCallable())
                    {
                        // try to go one level deeper
                        if (verbose())
                        {
                            Debug.WriteLine("entering " + it.name());
                        }
                        if (level < 10)
                        {
                            internalSetup(it.value(), ++level);
                        }
                        else
                        {
                            throw new NotSupportedException("setup of STP: too many nested levels (>=10) - check your syntax!");
                        }
                    }
                }
            }
            else
            {
                Debug.WriteLine("setup: not a valid javascript object.");
            }
        }

        public void dumpInfo()
        {
            if (GlobalSettings.instance().logLevelDebug())
            {
                return;
            }
            Debug.WriteLine(" ***************************************");
            Debug.WriteLine(" **************** Program dump for: " + name());
            Debug.WriteLine(" ***************************************");
            foreach (Activity act in mActivities) 
            {
                Debug.WriteLine("******* Activity *********");
                string info = String.Join('\n', act.info());
                Debug.WriteLine(info);
            }
        }

        private void setupActivity(QJSValue js_value, string name)
        {
            string type = js_value.property("type").toString();
            if (verbose())
            {
                Debug.WriteLine("setting up activity of type " + type + " from JS");
            }
            Activity act = Activity.createActivity(type, this);
            if (act == null)
            {
                return; // actually, an error is thrown in the previous call.
            }

            // use id-property if available, or the object-name otherwise
            act.setName(valueFromJs(js_value, "id", name).toString());
            // call the setup routine (overloaded version)
            act.setup(js_value);

            // call the onCreate handler:
            FomeScript.setActivity(act);
            act.events().run("onCreate", null);
            mActivities.Add(act);
        }

        private void clear()
        {
            mActivities.Clear();
            mEvents.clear();
            mActivityStand.Clear();
            mActivityNames.Clear();
            mSalvage = null;
            mOptions = new QJSValue(0); // clear
            mName = null;
        }

        public static QJSValue valueFromJs(QJSValue js_value, string key, string default_value = null, string errorMessage = null)
        {
            if (!js_value.hasOwnProperty(key))
            {
                if (String.IsNullOrEmpty(errorMessage) == false)
                {
                    throw new NotSupportedException(String.Format("Error: required key '{0}' not found. In: {1} (JS: {2})", key, errorMessage, FomeScript.JStoString(js_value)));
                }
                else if (String.IsNullOrEmpty(default_value))
                {
                    return new QJSValue();
                }
                else
                {
                    return new QJSValue(default_value);
                }
            }
            return (QJSValue)js_value.property(key);
        }

        public static bool boolValueFromJs(QJSValue js_value, string key, bool default_bool_value, string errorMessage = null)
        {
            if (!js_value.hasOwnProperty(key))
            {
                if (String.IsNullOrEmpty(errorMessage) == false)
                {
                    throw new NotSupportedException(String.Format("Error: required key '{0}' not found. In: {1} (JS: {2})", key, errorMessage, FomeScript.JStoString(js_value)));
                }
                else
                {
                    return default_bool_value;
                }
            }
            return js_value.property(key).toBool();
        }

        public static bool checkObjectProperties(QJSValue js_value, List<string> allowed_properties, string errorMessage)
        {
            QJSValueIterator it = new QJSValueIterator(js_value);
            bool found_issues = false;
            while (it.hasNext())
            {
                it.next();
                if (!allowed_properties.Contains(it.name()) && it.name() != "length")
                {
                    Debug.WriteLine("Syntax-warning: The javascript property " + it.name() + " is not used! In: " + errorMessage);
                    found_issues = true;
                }
            }
            return !found_issues;
        }
    }
}
