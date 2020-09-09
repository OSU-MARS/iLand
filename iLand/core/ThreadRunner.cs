using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace iLand.core
{
    /** @class ThreadRunner
        Encapsulates the invokation of multiple threads for paralellized tasks.
        To avoid lost updates during the light influence pattern application, all the resourceUnits
        are divided in two lists based on the index (even vs. uneven). These (for almost all cases)
        ensures, that no directly neighboring resourceUnits are processed.
        */
    internal class ThreadRunner
    {
        private static bool mMultithreaded = true; // static

        private List<ResourceUnit> mMap1, mMap2;
        private List<Species> mSpeciesMap;

        public bool multithreading() { return mMultithreaded; }
        public void setMultithreading(bool do_multithreading) { mMultithreaded = do_multithreading; }

        public ThreadRunner()
        {
            mMap1 = new List<ResourceUnit>();
            mMap2 = new List<ResourceUnit>();
        }

        public ThreadRunner(List<Species> speciesList)
        {
            setup(speciesList);
        }

        public void print()
        {
            Debug.WriteLine("Multithreading enabled: " + mMultithreaded + "thread count: " + System.Environment.ProcessorCount);
        }

        public void setup(List<ResourceUnit> resourceUnitList)
        {
            mMap1.Clear();
            mMap2.Clear();
            bool map = true;
            foreach (ResourceUnit unit in resourceUnitList)
            {
                if (map)
                {
                    mMap1.Add(unit);
                }
                else
                {
                    mMap2.Add(unit);
                }

                map = !map;
            }

        }

        public void setup(List<Species> speciesList) { mSpeciesMap = speciesList; }

        /// run a given function for each ressource unit either multithreaded or not.
        public void run(Action<ResourceUnit> funcptr, bool forceSingleThreaded = false)
        {
            if (mMultithreaded && mMap1.Count > 3 && forceSingleThreaded == false)
            {
                Parallel.ForEach(mMap1, (ResourceUnit unit) =>
                {
                    funcptr(unit);
                });
                Parallel.ForEach(mMap2, (ResourceUnit unit) =>
                {
                    funcptr(unit);
                });
            }
            else
            {
                // execute serialized in main thread
                foreach (ResourceUnit unit in mMap1)
                {
                    funcptr.Invoke(unit);
                }
                foreach (ResourceUnit unit in mMap2)
                {
                    funcptr.Invoke(unit);
                }
            }
        }

        /// run a given function for each species
        public void run(Action<Species> funcptr, bool forceSingleThreaded = false)
        {
            if (mMultithreaded && mSpeciesMap.Count > 3 && forceSingleThreaded == false)
            {
                Parallel.ForEach(mSpeciesMap, (Species species) =>
                {
                    funcptr.Invoke(species);
                });
            }
            else
            {
                // single threaded operation
                foreach (Species species in mSpeciesMap)
                {
                    funcptr.Invoke(species);
                }
            }
        }

        public void runGrid(Action<int, int> funcptr, int begin, int end, bool forceSingleThreaded, int minsize, int maxchunks)
        {
            int length = end - begin; // # of elements
            if (mMultithreaded && length > minsize * 3 && forceSingleThreaded == false)
            {
                int chunksize = minsize;
                if (length > chunksize * maxchunks)
                {
                    chunksize = length / maxchunks;
                }
                
                int chunks = length / chunksize;
                if (length - chunks * chunksize > 0)
                {
                    Debug.Assert(length - chunks * chunksize < chunksize);
                    ++chunks;
                }

                // execute operations
                Parallel.For(0, chunks, (int chunk) =>
                {
                    int p = begin + chunksize * chunk;
                    int pend = Math.Min(p + chunksize, end);
                    funcptr.Invoke(p, pend);
                });
            }
            else
            {
                // run all in one big function call
                funcptr.Invoke(begin, end);
            }
        }

        // multirunning function
        public void run<T>(Action<T> funcptr, List<T> container, bool forceSingleThreaded = false)
        {
            if (mMultithreaded && container.Count > 3 && forceSingleThreaded == false)
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
