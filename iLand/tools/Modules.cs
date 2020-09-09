using iLand.core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.tools
{
    internal class Modules
    {
        private List<DisturbanceInterface> mInterfaces; ///< the list stores only the active modules
        private List<SetupResourceUnitInterface> mSetupRUs;
        private List<WaterInterface> mWater;
        private List<TreeDeathInterface> mTreeDeath;

        public Modules()
        {
            init();
        }

        public bool hasSetupResourceUnits() { return mSetupRUs.Count != 0; }

        // load the static plugins
        private void init()
        {
            foreach (object plugin in QPluginLoader.staticInstances)
            {
                DisturbanceInterface di = plugin as DisturbanceInterface;
                if (di != null)
                {
                    Debug.WriteLine(di.name());
                    // check xml file
                    if (GlobalSettings.instance().settings().valueBool(String.Format("modules.{0}.enabled", di.name())))
                    {
                        // plugin is enabled: store in list of active modules
                        mInterfaces.Add(di);
                        // check for other interfaces
                        SetupResourceUnitInterface si = plugin as SetupResourceUnitInterface;
                        if (si != null)
                        {
                            mSetupRUs.Add(si);
                        }
                        WaterInterface wi = plugin as WaterInterface;
                        if (wi != null)
                        {
                            mWater.Add(wi);
                        }
                        TreeDeathInterface td = plugin as TreeDeathInterface;
                        if (td != null)
                        {
                            mTreeDeath.Add(td);
                        }
                    }
                }
            }

            // fix the order of modules: make sure that "barkbeetle" is after "wind"
            DisturbanceInterface wind = module("wind");
            DisturbanceInterface bb = module("barkbeetle");
            if (wind != null && bb != null)
            {
                int iw = mInterfaces.IndexOf(wind);
                int ib = mInterfaces.IndexOf(bb);
                if (ib < iw)
                {
                    // swap
                    DisturbanceInterface temp = mInterfaces[ib];
                    mInterfaces[ib] = mInterfaces[iw];
                    mInterfaces[iw] = temp;
                }
            }
        }

        public DisturbanceInterface module(string module_name)
        {
            foreach (DisturbanceInterface di in mInterfaces)
            {
                if (di.name() == module_name)
                {
                    return di;
                }
            }
            return null;
        }

        public void setupResourceUnit(ResourceUnit ru)
        {
            foreach (SetupResourceUnitInterface si in mSetupRUs)
            {
                si.setupResourceUnit(ru);
            }
        }

        public void setup()
        {
            foreach (DisturbanceInterface di in mInterfaces)
            {
                di.setup();
            }

            // set up the scripting (i.e., Javascript)
            QJSEngine engine = GlobalSettings.instance().scriptEngine();
            foreach (DisturbanceInterface di in mInterfaces)
            {
                di.setupScripting(engine);
            }
        }

        public void calculateWater(ResourceUnit resource_unit, WaterCycleData water_data)
        {
            foreach (WaterInterface wi in mWater)
            {
                wi.calculateWater(resource_unit, water_data);
            }
        }

        public void treeDeath(Tree tree, int removal_type)
        {
            if (mTreeDeath.Count == 0)
            {
                return;
            }

            for (int index = 0; index < mTreeDeath.Count; ++index)
            {
                mTreeDeath[index].treeDeath(tree, removal_type);
            }
        }

        public void run()
        {
            using DebugTimer t = new DebugTimer("modules");

            // *** run in fixed order ***
            foreach (DisturbanceInterface di in mInterfaces)
            {
                di.run();

                // *** run in random order ****
                //    List<DisturbanceInterface> run_list = mInterfaces;

                //    // execute modules in random order
                //    while (!run_list.isEmpty()) {
                //        int idx = irandom(0, run_list.size()-1);
                //        if (logLevelDebug())
                //            Debug.WriteLine("executing disturbance module: " << run_list[idx].name();

                //        try {
                //            run_list[idx].run();
                //        } catch (IException &e) {
                //            qWarning() << "ERROR: uncaught exception in module '" << run_list[idx].name() << "':";
                //            qWarning() << "ERROR:" << e.message();
                //            qWarning() << " **************************************** ";
                //        }

                //        // remove from list
                //        run_list.removeAt(idx);
                //    }
            }
        }

        public void yearBegin()
        {
            foreach (DisturbanceInterface di in mInterfaces)
            {
                di.yearBegin();
            }
        }
    }
}
