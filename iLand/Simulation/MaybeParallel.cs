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
    public class MaybeParallel<TLocal>
    {
        private readonly List<TLocal> workUnitCollection;

        public bool IsMultithreaded { get; set; }

        public MaybeParallel(List<TLocal> workUnits)
        {
            this.workUnitCollection = workUnits;
        }

        // run an action in parallel
        public void ForEach(Action<TLocal> action)
        {
            if (this.IsMultithreaded && (this.workUnitCollection.Count > 3))
            {
                
                Parallel.ForEach(this.workUnitCollection, (TLocal workUnit) =>
                {
                    action.Invoke(workUnit);
                });
            }
            else
            {
                // single threaded operation
                foreach (TLocal workUnit in this.workUnitCollection)
                {
                    action.Invoke(workUnit);
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
    }
}
