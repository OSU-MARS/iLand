using iLand.Tree;
using iLand.World;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace iLand.Simulation
{
    /** @class ThreadRunner
        Encapsulates the invokation of multiple threads for paralellized tasks.
        To avoid lost updates during the light influence pattern application, all the resourceUnits
        are divided in two lists based on the index (even vs. uneven). These (for almost all cases)
        ensures, that no directly neighboring resourceUnits are processed.
        */
    public class ThreadRunner
    {
        private readonly List<ResourceUnit> mResourceUnits;

        public bool IsMultithreaded { get; set; }
        public List<Species> Species { get; set; }

        public ThreadRunner()
        {
            this.mResourceUnits = new List<ResourceUnit>();

            this.IsMultithreaded = true;
            this.Species = null;
        }

        public ThreadRunner(List<Species> speciesList)
            : this()
        {
            this.Species = speciesList;
        }

        public void Setup(List<ResourceUnit> resourceUnits)
        {
            this.mResourceUnits.Clear();
            this.mResourceUnits.AddRange(resourceUnits);
        }

        /// run a given function for each ressource unit either multithreaded or not.
        public void Run(Action<ResourceUnit> funcptr, bool forceSingleThreaded = false)
        {
            this.Run(funcptr, this.mResourceUnits, forceSingleThreaded);
        }

        /// run a given function for each species
        public void Run(Action<Species, Model> funcptr, Model model, bool forceSingleThreaded = false)
        {
            if (this.IsMultithreaded && this.Species.Count > 3 && forceSingleThreaded == false)
            {
                Parallel.ForEach(Species, (Species species) =>
                {
                    funcptr.Invoke(species, model);
                });
            }
            else
            {
                // single threaded operation
                foreach (Species species in this.Species)
                {
                    funcptr.Invoke(species, model);
                }
            }
        }

        //public void RunGrid(Action<int, int> funcptr, int begin, int end, bool forceSingleThreaded, int minsize, int maxchunks)
        //{
        //    int length = end - begin; // # of elements
        //    if (IsMultithreaded && length > minsize * 3 && forceSingleThreaded == false)
        //    {
        //        int chunksize = minsize;
        //        if (length > chunksize * maxchunks)
        //        {
        //            chunksize = length / maxchunks;
        //        }
                
        //        int chunks = length / chunksize;
        //        if (length - chunks * chunksize > 0)
        //        {
        //            Debug.Assert(length - chunks * chunksize < chunksize);
        //            ++chunks;
        //        }

        //        // execute operations
        //        Parallel.For(0, chunks, (int chunk) =>
        //        {
        //            int p = begin + chunksize * chunk;
        //            int pend = Math.Min(p + chunksize, end);
        //            funcptr.Invoke(p, pend);
        //        });
        //    }
        //    else
        //    {
        //        // run all in one big function call
        //        funcptr.Invoke(begin, end);
        //    }
        //}

        public void Run<T>(Action<T> funcptr, List<T> container, bool forceSingleThreaded = false)
        {
            if (this.IsMultithreaded && container.Count > 3 && forceSingleThreaded == false)
            {
                Parallel.ForEach(container, (T element) =>
                {
                    funcptr.Invoke(element);
                });
            }
            else
            {
                // execute serialized in main thread
                foreach (T element in container)
                {
                    funcptr.Invoke(element);
                }
            }
        }
    }
}
