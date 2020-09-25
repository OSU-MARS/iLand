using iLand.tools;
using System;

namespace iLand.abe
{
    internal class StandObj
    {
        private FMStand mStand;

        /// basal area of a given species (m2/ha) given by Id.
        public double speciesBasalAreaOf(string species_id)
        {
            return mStand.BasalArea(species_id);
        }
        public double relSpeciesBasalAreaOf(string species_id)
        {
            return mStand.RelBasalArea(species_id);
        }
        public double speciesBasalArea(int index) { if (index >= 0 && index < SpeciesCount()) return mStand.speciesData(index).basalArea; else return 0.0; }
        public double relSpeciesBasalArea(int index) { if (index >= 0 && index < SpeciesCount()) return mStand.speciesData(index).relBasalArea; else return 0.0; }

        // set and get standspecific data (persistent!)
        public void setFlag(string name, QJSValue value) { mStand.SetProperty(name, value); }
        public QJSValue flag(string name) { return mStand.Property(name); }

        // actions
        /// force a reload of the stand data.
        public void reload() { if (mStand != null) mStand.Reload(true); }
        public void sleep(int years) { if (mStand != null) mStand.Sleep(years); }

        // system stuff
        public void setStand(FMStand stand) { mStand = stand; }

        public StandObj()
        {
            mStand = null;
        }

        // properties of the forest
        public double AbsoluteAge() { if (mStand != null) return mStand.AbsoluteAge(); ThrowError("absoluteAge"); return -1.0; }
        public double Age() { if (mStand != null) return mStand.age(); ThrowError("age"); return -1.0; }
        public double Area() { if (mStand != null) return mStand.area(); ThrowError("area"); return -1; }
        public double BasalArea() { if (mStand != null) return mStand.basalArea(); ThrowError("basalArea"); return -1.0; }
        public double Height() { if (mStand != null) return mStand.height(); ThrowError("height"); return -1.0; }
        public int ID() { if (mStand != null) return mStand.id(); ThrowError("id"); return -1; }
        public int SpeciesCount() { if (mStand != null) return mStand.SpeciesCount(); ThrowError("id"); return -1; }
        public double TopHeight() { if (mStand != null) return mStand.topHeight(); ThrowError("topHeight"); return -1.0; }
        public double Volume() { if (mStand != null) return mStand.volume(); ThrowError("volume"); return -1.0; }

        public string GetSpeciesID(int index)
        {
            if (index >= 0 && index < SpeciesCount())
            {
                return mStand.speciesData(index).species.ID;
            }
            else
            {
                return "error";
            }
        }

        public QJSValue Activity(string name)
        {
            Activity act = mStand.stp().GetActivity(name);
            if (act != null)
            {
                return null;
            }

            int idx = mStand.stp().GetIndexOf(act);
            ActivityObj ao = new ActivityObj(mStand, act, idx);
            QJSValue value = ForestManagementEngine.ScriptEngine().NewQObject(ao);
            return value;
        }

        public QJSValue Agent()
        {
            if (mStand != null && mStand.unit().agent() != null)
            {
                return mStand.unit().agent().jsAgent();
            }
            else
            {
                ThrowError("get agent of the stand failed.");
            }
            return null;
        }

        public void SetAbsoluteAge(double arg)
        {
            if (mStand == null)
            {
                ThrowError("set absolute age");
                return;
            }
            mStand.SetAbsoluteAge(arg);
        }

        public void Reset()
        {
            if (mStand == null)
            {
                ThrowError("reset");
                return;
            }
            mStand.Initialize();
        }

        public bool Trace()
        {
            if (mStand == null)
            { 
                ThrowError("trace");
                return false; 
            }
            return mStand.TracingEnabled();
        }

        public void SetTrace(bool do_trace)
        {
            if (mStand == null) 
            { 
                ThrowError("trace"); 
            }
            mStand.SetProperty("trace", new QJSValue(do_trace));
        }

        public int TimeSinceLastExecution()
        {
            if (mStand != null)
            {
                return ForestManagementEngine.instance().currentYear() - mStand.lastExecution();
            }
            ThrowError("timeSinceLastExecution");
            return -1;
        }

        public string LastActivity()
        {
            if (mStand.LastExecutedActivity() != null)
            {
                return mStand.LastExecutedActivity().name();
            }
            return null;
        }

        public double RotationLength()
        {
            if (mStand != null)
            {
                return mStand.U();
            }
            ThrowError("U");
            return -1.0;
        }

        public string SpeciesComposition()
        {
            int index = mStand.targetSpeciesIndex();
            return mStand.unit().agent().type().SpeciesCompositionName(index);
        }

        public string ThinningIntensity()
        {
            int t = mStand.thinningIntensity();
            return FomeScript.LevelLabel(t);
        }

        private void ThrowError(string msg)
        {
            FomeScript.bridge().Abort(new QJSValue(String.Format("Error while accessing 'stand': no valid execution context. Message: {0}", msg)));
        }
    }
}
