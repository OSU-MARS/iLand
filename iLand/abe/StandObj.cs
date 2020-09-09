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
            return mStand.basalArea(species_id);
        }
        public double relSpeciesBasalAreaOf(string species_id)
        {
            return mStand.relBasalArea(species_id);
        }
        public double speciesBasalArea(int index) { if (index >= 0 && index < nspecies()) return mStand.speciesData(index).basalArea; else return 0.0; }
        public double relSpeciesBasalArea(int index) { if (index >= 0 && index < nspecies()) return mStand.speciesData(index).relBasalArea; else return 0.0; }

        // set and get standspecific data (persistent!)
        public void setFlag(string name, QJSValue value) { mStand.setProperty(name, value); }
        public QJSValue flag(string name) { return mStand.property(name); }

        // actions
        /// force a reload of the stand data.
        public void reload() { if (mStand != null) mStand.reload(true); }
        public void sleep(int years) { if (mStand != null) mStand.sleep(years); }

        public StandObj(object parent = null)
        {
            mStand = null;
        }

        // system stuff
        public void setStand(FMStand stand) { mStand = stand; }

        // properties of the forest
        public double basalArea() { if (mStand != null) return mStand.basalArea(); throwError("basalArea"); return -1.0; }
        public double height() { if (mStand != null) return mStand.height(); throwError("height"); return -1.0; }
        public double topHeight() { if (mStand != null) return mStand.topHeight(); throwError("topHeight"); return -1.0; }
        public double age() { if (mStand != null) return mStand.age(); throwError("age"); return -1.0; }
        public double absoluteAge() { if (mStand != null) return mStand.absoluteAge(); throwError("absoluteAge"); return -1.0; }
        public double volume() { if (mStand != null) return mStand.volume(); throwError("volume"); return -1.0; }
        public int id() { if (mStand != null) return mStand.id(); throwError("id"); return -1; }
        public int nspecies() { if (mStand != null) return mStand.nspecies(); throwError("id"); return -1; }
        public double area() { if (mStand != null) return mStand.area(); throwError("area"); return -1; }

        public string speciesId(int index)
        {
            if (index >= 0 && index < nspecies())
            {
                return mStand.speciesData(index).species.id();
            }
            else
            {
                return "error";
            }
        }

        public QJSValue activity(string name)
        {
            Activity act = mStand.stp().activity(name);
            if (act != null)
            {
                return null;
            }

            int idx = mStand.stp().activityIndex(act);
            ActivityObj ao = new ActivityObj(mStand, act, idx);
            QJSValue value = ForestManagementEngine.scriptEngine().newQObject(ao);
            return value;
        }

        public QJSValue agent()
        {
            if (mStand != null && mStand.unit().agent() != null)
            {
                return mStand.unit().agent().jsAgent();
            }
            else
            {
                throwError("get agent of the stand failed.");
            }
            return null;
        }

        public void setAbsoluteAge(double arg)
        {
            if (mStand == null)
            {
                throwError("set absolute age");
                return;
            }
            mStand.setAbsoluteAge(arg);
        }

        public void reset()
        {
            if (mStand == null)
            {
                throwError("reset");
                return;
            }
            mStand.initialize();
        }

        public bool trace()
        {
            if (mStand == null)
            { 
                throwError("trace");
                return false; 
            }
            return mStand.trace();
        }

        public void setTrace(bool do_trace)
        {
            if (mStand == null) 
            { 
                throwError("trace"); 
            }
            mStand.setProperty("trace", new QJSValue(do_trace));
        }

        public int timeSinceLastExecution()
        {
            if (mStand != null)
            {
                return ForestManagementEngine.instance().currentYear() - mStand.lastExecution();
            }
            throwError("timeSinceLastExecution");
            return -1;
        }

        public string lastActivity()
        {
            if (mStand.lastExecutedActivity() != null)
            {
                return mStand.lastExecutedActivity().name();
            }
            return null;
        }

        public double rotationLength()
        {
            if (mStand != null)
            {
                return mStand.U();
            }
            throwError("U");
            return -1.0;
        }

        public string speciesComposition()
        {
            int index = mStand.targetSpeciesIndex();
            return mStand.unit().agent().type().speciesCompositionName(index);
        }

        public string thinningIntensity()
        {
            int t = mStand.thinningIntensity();
            return FomeScript.levelLabel(t);
        }

        private void throwError(string msg)
        {
            FomeScript.bridge().abort(new QJSValue(String.Format("Error while accessing 'stand': no valid execution context. Message: {0}", msg)));
        }
    }
}
