using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;

namespace iLand.Plugin
{
    public class Modules
    {
        private readonly List<IDisturbanceInterface> disturbanceModules; // the list stores only the active modules
        private readonly List<ITreeDeathInterface> mortalityModules;
        private readonly List<ISetupResourceUnitInterface> resourceUnitSetupModules;
        private readonly List<IWaterInterface> waterModules;

        public Modules()
        {
            this.disturbanceModules = new();
            this.mortalityModules = new();
            this.resourceUnitSetupModules = new();
            this.waterModules = new();

            foreach (IDisturbanceInterface modules in PluginLoader.StaticInstances)
            {
                // plugin is enabled: store in list of active modules
                this.disturbanceModules.Add(modules);
                // check for other interfaces
                if (modules is ISetupResourceUnitInterface setupResourceUnit)
                {
                    this.resourceUnitSetupModules.Add(setupResourceUnit);
                }
                if (modules is IWaterInterface water)
                {
                    this.waterModules.Add(water);
                }
                if (modules is ITreeDeathInterface treeDeath)
                {
                    this.mortalityModules.Add(treeDeath);
                }
            }

            // ensure order of disturbance modules: make sure bark beetles follow wind
            // TODO: this should probably throw instead of silently repairing project file
            IDisturbanceInterface? wind = this.GetDisturbanceModule("wind");
            IDisturbanceInterface? beetles = this.GetDisturbanceModule("barkbeetle");
            if ((wind != null) && (beetles != null))
            {
                int windIndex = this.disturbanceModules.IndexOf(wind);
                int beetleIndex = this.disturbanceModules.IndexOf(beetles);
                if (beetleIndex < windIndex)
                {
                    // swap
                    (disturbanceModules[windIndex], disturbanceModules[beetleIndex]) = (disturbanceModules[beetleIndex], disturbanceModules[windIndex]);
                }
            }
        }

        public void CalculateWater(ResourceUnit resourceUnit)
        {
            for (int waterIndex = 0; waterIndex < this.waterModules.Count; ++waterIndex)
            {
                this.waterModules[waterIndex].CalculateWater(resourceUnit);
            }
        }

        private IDisturbanceInterface? GetDisturbanceModule(string moduleName)
        {
            for (int disturbanceIndex = 0; disturbanceIndex < this.disturbanceModules.Count; ++disturbanceIndex)
            {
                IDisturbanceInterface module = this.disturbanceModules[disturbanceIndex];
                if (String.Equals(module.Name, moduleName, StringComparison.Ordinal))
                {
                    return module;
                }
            }
            return null;
        }

        public bool HasResourceUnitSetup()
        {
            return this.resourceUnitSetupModules.Count != 0;
        }

        public void OnTreeDeath(Trees tree, MortalityCause mortalityCause)
        {
            for (int mortalityIndex = 0; mortalityIndex < mortalityModules.Count; ++mortalityIndex)
            {
                this.mortalityModules[mortalityIndex].OnTreeDeath(tree, mortalityCause);
            }
        }

        public void SetupDisturbances()
        {
            for (int disturbanceIndex = 0; disturbanceIndex < this.disturbanceModules.Count; ++disturbanceIndex)
            {
                this.disturbanceModules[disturbanceIndex].Setup();
            }
        }

        public void SetupResourceUnit(ResourceUnit resourceUnit)
        {
            for (int resourceSetupIndex = 0; resourceSetupIndex < this.resourceUnitSetupModules.Count; ++resourceSetupIndex)
            {
                this.resourceUnitSetupModules[resourceSetupIndex].SetupResourceUnit(resourceUnit);
            }
        }

        public void RunYear()
        {
            // *** run in fixed order ***
            for (int disturbanceIndex = 0; disturbanceIndex < this.disturbanceModules.Count; ++disturbanceIndex)
            {
                this.disturbanceModules[disturbanceIndex].Run();

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

        public void OnStartYear()
        {
            for (int disturbanceIndex = 0; disturbanceIndex < this.disturbanceModules.Count; ++disturbanceIndex)
            {
                this.disturbanceModules[disturbanceIndex].OnStartYear();
            }
        }
    }
}
