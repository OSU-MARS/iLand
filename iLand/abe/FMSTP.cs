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
        private readonly Events mEvents;
        private bool mHasRepeatingActivities; ///< true, if the STP contains repeating activities
        private readonly List<Activity> mActivities; ///< container for the activities of the STP
        private readonly List<ActivityFlags> mActivityStand; ///< base data for stand-specific STP info.
        private readonly List<string> mActivityNames;  ///< names of all available activities
        // special activities
        private ActSalvage mSalvage;

        // STP-level properties
        private readonly int[] mRotationLength; ///< three levels (low, medium,high) of rotation length

        private QJSValue mOptions; ///< user-defined options of the STP

        public string name() { return mName; }

        /// defaultFlags() is used to initalized the flags for indiv. forest stands
        public List<ActivityFlags> defaultFlags() { return mActivityStand; }
        public Events events() { return mEvents; }
        public QJSValue JSoptions() { return mOptions; }

        public ActSalvage salvageActivity() { return mSalvage; }

        /// if verbose is true, detailed debug information is provided.
        public static void setVerbose(bool verbose) { mVerbose = verbose; }
        public static bool verbose() { return mVerbose; } ///< returns true in debug mode

        public FMSTP()
        {
            this.mActivities = new List<Activity>();
            this.mActivityNames = new List<string>();
            this.mActivityStand = new List<ActivityFlags>();
            this.mEvents = new Events();
            this.mOptions = new QJSValue(0);
            this.mRotationLength = new int[3];
            this.mRotationLength[0] = 90; // sensible defaults
            this.mRotationLength[1] = 100;
            this.mRotationLength[2] = 110;
            this.mSalvage = null;
        }

        public int GetIndexOf(Activity act) { return mActivities.IndexOf(act); }
        /// rotation length (years)
        public int GetRotationLength(int type) { if (type > 0 && type < 4) return mRotationLength[type - 1]; return 0; }
        public int GetRotationType(int length) { for (int i = 0; i < 3; ++i) if (mRotationLength[i] == length) return i + 1; return -1; } // TODO: fix

        public Activity GetActivity(string name)
        {
            int idx = mActivityNames.IndexOf(name);
            if (idx == -1)
            {
                return null;
            }
            return mActivities[idx];
        }

        public int ActivityScheduledEarlier(Activity a, Activity b)
        {
            if (a.GetEarlistSchedule() < b.GetEarlistSchedule())
            {
                return -1;
            }
            if (a.GetEarlistSchedule() > b.GetEarlistSchedule())
            {
                return 1;
            }
            return 0;
        }

        public void Setup(QJSValue js_value, string name)
        {
            Clear();

            if (String.IsNullOrEmpty(name) == false)
            {
                mName = name;
            }

            // (1) scan recursively the data structure and create
            //     all activites
            InternalSetup(js_value, 0);

            // (2) create all other required meta information (such as ActivityStand)
            // sort activites based on the minimum execution time
            mActivities.Sort(ActivityScheduledEarlier);

            mActivityNames.Clear();
            mHasRepeatingActivities = false;
            for (int i = 0; i < mActivities.Count; ++i)
            {
                mActivityNames.Add(mActivities[i].name());
                mActivityStand.Add(mActivities[i].StandFlags()); // stand = null: create a copy of the activities' base flags
                mActivities[i].setIndex(i);
                if (mActivities[i].IsRepeating())
                {
                    mHasRepeatingActivities = true;
                }
                if (mActivities[i].StandFlags().IsSalvage())
                {
                    mSalvage = (ActSalvage)mActivities[i];
                    mHasRepeatingActivities = false;
                }
            }

            // (3) set up top-level events
            mEvents.Setup(js_value, new List<string>() { "onInit", "onExit" });
        }

        public bool ExecuteRepeatingActivities(FMStand stand)
        {
            if (mSalvage != null)
            {
                if (stand.TotalHarvest() != 0.0 || stand.Property("_run_salvage").ToBool())
                {
                    // at this point totalHarvest is only disturbance related harvests.
                    stand.ExecuteActivity(mSalvage);
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
                    if (!stand.flags(i).IsActive() || !stand.flags(i).IsEnabled())
                    {
                        continue;
                    }
                    if (stand.TracingEnabled())
                    {
                        Debug.WriteLine("running repeating activity " + mActivities[i].name());
                    }
                    result |= stand.ExecuteActivity(mActivities[i]);
                }
            }

            return result; // return true if at least one repeating activity was executed.
        }

        public void EvaluateDynamicExpressions(FMStand stand)
        {
            foreach (Activity act in mActivities)
        {
                act.EvaluateDyanamicExpressions(stand);
            }
        }

        // read the setting from the setup-javascript object
        private void InternalSetup(QJSValue js_value, int level)
        {
            // top-level
            if (js_value.HasOwnProperty("schedule"))
            {
                SetupActivity(js_value, "unnamed");
                return;
            }

            // nested objects
            if (js_value.IsObject())
            {
                QJSValueIterator it = new QJSValueIterator(js_value);
                while (it.HasNext())
                {
                    it.Next();
                    // parse special properties
                    if (it.Name() == "U" && it.Value().IsArray())
                    {
                        List<int> list = (List<int>)it.Value().ToVariant();
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
                    if (it.Name() == "options")
                    {
                        mOptions = it.Value();
                        continue;
                    }
                    if (it.Value().HasOwnProperty("type"))
                    {
                        // try to set up as activity
                        SetupActivity(it.Value(), it.Name());
                    }
                    else if (it.Value().IsObject() && !it.Value().IsCallable())
                    {
                        // try to go one level deeper
                        if (verbose())
                        {
                            Debug.WriteLine("entering " + it.Name());
                        }
                        if (level < 10)
                        {
                            InternalSetup(it.Value(), ++level);
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

        public void DumpInfo()
        {
            if (GlobalSettings.Instance.LogDebug())
            {
                return;
            }
            Debug.WriteLine(" ***************************************");
            Debug.WriteLine(" **************** Program dump for: " + name());
            Debug.WriteLine(" ***************************************");
            foreach (Activity act in mActivities) 
            {
                Debug.WriteLine("******* Activity *********");
                string info = String.Join('\n', act.Info());
                Debug.WriteLine(info);
            }
        }

        private void SetupActivity(QJSValue js_value, string name)
        {
            string type = js_value.Property("type").ToString();
            if (verbose())
            {
                Debug.WriteLine("setting up activity of type " + type + " from JS");
            }
            Activity act = Activity.CreateActivity(type);
            if (act == null)
            {
                return; // actually, an error is thrown in the previous call.
            }

            // use id-property if available, or the object-name otherwise
            act.setName(ValueFromJS(js_value, "id", name).ToString());
            // call the setup routine (overloaded version)
            act.Setup(js_value);

            // call the onCreate handler:
            FomeScript.SetActivity(act);
            act.events().Run("onCreate", null);
            mActivities.Add(act);
        }

        private void Clear()
        {
            mActivities.Clear();
            mEvents.Clear();
            mActivityStand.Clear();
            mActivityNames.Clear();
            mSalvage = null;
            mOptions = new QJSValue(0); // clear
            mName = null;
        }

        public static QJSValue ValueFromJS(QJSValue js_value, string key, string default_value = null, string errorMessage = null)
        {
            if (!js_value.HasOwnProperty(key))
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
            return (QJSValue)js_value.Property(key);
        }

        public static bool BoolValueFromJS(QJSValue js_value, string key, bool default_bool_value, string errorMessage = null)
        {
            if (!js_value.HasOwnProperty(key))
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
            return js_value.Property(key).ToBool();
        }

        public static bool CheckObjectProperties(QJSValue js_value, List<string> allowed_properties, string errorMessage)
        {
            QJSValueIterator it = new QJSValueIterator(js_value);
            bool found_issues = false;
            while (it.HasNext())
            {
                it.Next();
                if (!allowed_properties.Contains(it.Name()) && it.Name() != "length")
                {
                    Debug.WriteLine("Syntax-warning: The javascript property " + it.Name() + " is not used! In: " + errorMessage);
                    found_issues = true;
                }
            }
            return !found_issues;
        }
    }
}
