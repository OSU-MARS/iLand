using iLand.Trees;
using iLand.World;
using System;
using System.Collections.Generic;

namespace iLand.Plugin
{
    public class Modules
    {
        private readonly List<IDisturbanceInterface> mModules; // the list stores only the active modules
        private readonly List<ISetupResourceUnitInterface> mSetupRUs;
        private readonly List<ITreeDeathInterface> mTreeDeath;
        private readonly List<IWaterInterface> mWater;

        public Modules()
        {
            this.mModules = new List<IDisturbanceInterface>();
            this.mSetupRUs = new List<ISetupResourceUnitInterface>();
            this.mTreeDeath = new List<ITreeDeathInterface>();
            this.mWater = new List<IWaterInterface>();

            foreach (IDisturbanceInterface modules in PluginLoader.StaticInstances)
            {
                // plugin is enabled: store in list of active modules
                mModules.Add(modules);
                // check for other interfaces
                if (modules is ISetupResourceUnitInterface setupResourceUnit)
                {
                    mSetupRUs.Add(setupResourceUnit);
                }
                if (modules is IWaterInterface water)
                {
                    mWater.Add(water);
                }
                if (modules is ITreeDeathInterface treeDeath)
                {
                    mTreeDeath.Add(treeDeath);
                }
            }

            // fix the order of modules: make sure that "barkbeetle" is after "wind"
            IDisturbanceInterface wind = GetModule("wind");
            IDisturbanceInterface beetles = GetModule("barkbeetle");
            if (wind != null && beetles != null)
            {
                int windIndex = mModules.IndexOf(wind);
                int beetleIndex = mModules.IndexOf(beetles);
                if (beetleIndex < windIndex)
                {
                    // swap
                    IDisturbanceInterface temp = mModules[beetleIndex];
                    mModules[beetleIndex] = mModules[windIndex];
                    mModules[windIndex] = temp;
                }
            }
        }

        public bool HasSetupResourceUnits() { return mSetupRUs.Count != 0; }

        public IDisturbanceInterface GetModule(string moduleName)
        {
            foreach (IDisturbanceInterface module in mModules)
            {
                if (String.Equals(module.Name(), moduleName, StringComparison.Ordinal))
                {
                    return module;
                }
            }
            return null;
        }

        public void SetupResourceUnit(ResourceUnit ru)
        {
            foreach (ISetupResourceUnitInterface setupResourceUnit in mSetupRUs)
            {
                setupResourceUnit.SetupResourceUnit(ru);
            }
        }

        public void SetupDisturbances()
        {
            foreach (IDisturbanceInterface module in mModules)
            {
                module.Setup();
            }
        }

        public void CalculateWater(ResourceUnit resourceUnit, WaterCycleData waterData)
        {
            foreach (IWaterInterface water in mWater)
            {
                water.CalculateWater(resourceUnit, waterData);
            }
        }

        public void TreeDeath(Tree tree, MortalityCause mortalityCause)
        {
            for (int index = 0; index < mTreeDeath.Count; ++index)
            {
                mTreeDeath[index].TreeDeath(tree, mortalityCause);
            }
        }

        public void Run()
        {
            //using DebugTimer t = model.DebugTimers.Create("Modules.Run()");

            // *** run in fixed order ***
            foreach (IDisturbanceInterface module in mModules)
            {
                module.Run();

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
            foreach (IDisturbanceInterface module in mModules)
            {
                module.YearBegin();
            }
        }
    }
}
