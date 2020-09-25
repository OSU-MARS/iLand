using iLand.core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace iLand.tools
{
    internal class Modules
    {
        private readonly List<IDisturbanceInterface> mInterfaces; ///< the list stores only the active modules
        private readonly List<ISetupResourceUnitInterface> mSetupRUs;
        private readonly List<ITreeDeathInterface> mTreeDeath;
        private readonly List<IWaterInterface> mWater;

        public Modules()
        {
            this.mInterfaces = new List<IDisturbanceInterface>();
            this.mSetupRUs = new List<ISetupResourceUnitInterface>();
            this.mTreeDeath = new List<ITreeDeathInterface>();
            this.mWater = new List<IWaterInterface>();

            this.Init();
        }

        public bool HasSetupResourceUnits() { return mSetupRUs.Count != 0; }

        // load the static plugins
        private void Init()
        {
            foreach (object plugin in QPluginLoader.StaticInstances)
            {
                if (plugin is IDisturbanceInterface di)
                {
                    Debug.WriteLine(di.Name());
                    // check xml file
                    if (GlobalSettings.Instance.Settings.ValueBool(String.Format("modules.{0}.enabled", di.Name())))
                    {
                        // plugin is enabled: store in list of active modules
                        mInterfaces.Add(di);
                        // check for other interfaces
                        if (plugin is ISetupResourceUnitInterface si)
                        {
                            mSetupRUs.Add(si);
                        }
                        if (plugin is IWaterInterface wi)
                        {
                            mWater.Add(wi);
                        }
                        if (plugin is ITreeDeathInterface td)
                        {
                            mTreeDeath.Add(td);
                        }
                    }
                }
            }

            // fix the order of modules: make sure that "barkbeetle" is after "wind"
            IDisturbanceInterface wind = Module("wind");
            IDisturbanceInterface bb = Module("barkbeetle");
            if (wind != null && bb != null)
            {
                int iw = mInterfaces.IndexOf(wind);
                int ib = mInterfaces.IndexOf(bb);
                if (ib < iw)
                {
                    // swap
                    IDisturbanceInterface temp = mInterfaces[ib];
                    mInterfaces[ib] = mInterfaces[iw];
                    mInterfaces[iw] = temp;
                }
            }
        }

        public IDisturbanceInterface Module(string module_name)
        {
            foreach (IDisturbanceInterface di in mInterfaces)
            {
                if (di.Name() == module_name)
                {
                    return di;
                }
            }
            return null;
        }

        public void SetupResourceUnit(ResourceUnit ru)
        {
            foreach (ISetupResourceUnitInterface si in mSetupRUs)
            {
                si.SetupResourceUnit(ru);
            }
        }

        public void Setup()
        {
            foreach (IDisturbanceInterface di in mInterfaces)
            {
                di.Setup();
            }

            // set up the scripting (i.e., Javascript)
            QJSEngine engine = GlobalSettings.Instance.ScriptEngine;
            foreach (IDisturbanceInterface di in mInterfaces)
            {
                di.SetupScripting(engine);
            }
        }

        public void CalculateWater(ResourceUnit resource_unit, WaterCycleData water_data)
        {
            foreach (IWaterInterface wi in mWater)
            {
                wi.CalculateWater(resource_unit, water_data);
            }
        }

        public void TreeDeath(Tree tree, int removal_type)
        {
            if (mTreeDeath.Count == 0)
            {
                return;
            }

            for (int index = 0; index < mTreeDeath.Count; ++index)
            {
                mTreeDeath[index].TreeDeath(tree, removal_type);
            }
        }

        public void Run()
        {
            using DebugTimer t = new DebugTimer("modules");

            // *** run in fixed order ***
            foreach (IDisturbanceInterface di in mInterfaces)
            {
                di.Run();

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

        public void YearBegin()
        {
            foreach (IDisturbanceInterface di in mInterfaces)
            {
                di.YearBegin();
            }
        }
    }
}
