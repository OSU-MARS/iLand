using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace iLand.Simulation
{
    /** Encapsulates the invokation of multiple threads for paralellized tasks.
        To avoid lost updates during the light influence pattern application, all the resourceUnits
        are divided in two lists based on the index (even vs. uneven). These (for almost all cases)
        ensures, that no directly neighboring resourceUnits are processed.
        */
    public class MaybeParallel<TItem>
    {
        private readonly List<TItem> parallelizableItems;
        private readonly ParallelOptions parallelOptions;

        public MaybeParallel(List<TItem> parallelizableItems, int maximumThreads)
        {
            this.parallelizableItems = parallelizableItems;
            this.parallelOptions = new()
            {
                MaxDegreeOfParallelism = maximumThreads
            };
        }

        // run an action in parallel
        public void For(Action<TItem> action)
        {
            if ((this.parallelOptions.MaxDegreeOfParallelism > 1) && (this.parallelizableItems.Count > 3))
            {
                Parallel.For(0, this.parallelizableItems.Count, this.parallelOptions, (int index) =>
                {
                    action.Invoke(this.parallelizableItems[index]);
                });
            }
            else
            {
                // single threaded operation
                foreach (TItem workUnit in this.parallelizableItems)
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
        //            int pend = MathF.Min(p + chunksize, end);
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
