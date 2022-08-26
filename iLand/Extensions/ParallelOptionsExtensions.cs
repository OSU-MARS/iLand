using System.Diagnostics;
using System;
using System.Threading.Tasks;

namespace iLand.Extensions
{
    internal static class ParallelOptionsExtensions
    {
        public static (int threads, int itemsPerThread) GetUniformPartitioning(this ParallelOptions parallelOptions, int itemCount, int minimumItemCountPerPartition)
        {
            int partitions = 1;
            int itemsPerPartition = itemCount;
            if (parallelOptions.MaxDegreeOfParallelism > 1)
            {
                itemsPerPartition /= parallelOptions.MaxDegreeOfParallelism - 1;
                if (itemsPerPartition < minimumItemCountPerPartition)
                {
                    itemsPerPartition = minimumItemCountPerPartition;
                }
                partitions = (int)MathF.Ceiling((float)itemCount / itemsPerPartition);
            }
            Debug.Assert(partitions * itemsPerPartition >= itemCount);

            return (partitions, itemsPerPartition);
        }

        public static (int startIndex, int endIndex) GetUniformPartitionRange(int threadIndex, int itemsPerThread, int maxItemIndex)
        {
            int startIndex = itemsPerThread * threadIndex;
            int endIndex = startIndex + itemsPerThread;
            if (endIndex > maxItemIndex)
            {
                endIndex = maxItemIndex;
            }

            return (startIndex, endIndex);
        }
    }
}
