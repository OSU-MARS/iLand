using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace iLand.Tool
{
    // Helper class for statistics. This class calculates from a float-vector relevant information used for box plots.
    internal class SummaryStatistics
    {
        private List<float> data; // to allow late calculation of percentiles (e.g. a call to "median()".)
        private float percentile25;
        private float percentile75;
        private float median;
        private float standardDeviation; // standard deviation

        public float Sum { get; private set; } // sum of values
        public float Mean { get; private set; } // arithmetic mean
        public float Min { get; private set; } // minimum value
        public float Max { get; private set; } // maximum value

        public SummaryStatistics()
            : this([])
        {
        }

        public SummaryStatistics(List<float> data)
        {
            this.SetData(data);
        }

        public float GetMedian()
        {
            if (Single.IsNaN(this.median))
            {
                this.median = this.GetPercentile(50); // 2nd quartile = median
            }
            return this.median;
        }

        public float GetPercentile(int percentile)
        {
            // code von: Fast median search: an ANSI C implementation, Nicolas Devillard, http://ndevilla.free.fr/median/median/index.html
            // algo. kommt von Wirth, hier nur an c++ angepasst.
            if ((percentile < 1) || (percentile > 99))
            {
                throw new ArgumentOutOfRangeException(nameof(percentile));
            }
            if (this.data.Count == 0)
            {
                throw new NotSupportedException();
            }

            int k;
            int n = this.data.Count;
            // k ist der "Index" des gesuchten wertes
            if (percentile != 50)
            {
                // irgendwelche perzentillen
                int d = 100 / (percentile > 50 ? (100 - percentile) : percentile);
                k = n / d;
                if (percentile > 50)
                {
                    k = n - k - 1;
                }
            }
            else
            {
                // median
                if ((n & 1) == 1)  // gerade/ungerade?
                {
                    k = n / 2;  // mittlerer wert
                }
                else
                {
                    k = n / 2 - 1; // wert unter der mitte
                }
            }

            int l = 0; 
            int m = n - 1;

            while (l < m)
            {
                float x = this.data[k];
                int i = l;
                int j = m;
                do
                {
                    while (this.data[i] < x)
                    {
                        ++i;
                    }
                    while (x < this.data[j])
                    {
                        --j;
                    }
                    if (i <= j)
                    {
                        (this.data[j], this.data[i]) = (this.data[i], this.data[j]); // swap elements
                        i++; 
                        j--;
                    }
                } while (i <= j);

                if (j < k)
                {
                    l = i;
                }
                if (k < i)
                {
                    m = j;
                }
            }

            return this.data[k];
        }

        public float GetPercentile25()
        {
            if (Single.IsNaN(this.percentile25))
            {
                this.percentile25 = this.GetPercentile(25);
            }
            return this.percentile25;
        }

        public float GetPercentile75()
        {
            if (Single.IsNaN(this.percentile75))
            {
                this.percentile75 = this.GetPercentile(75);
            }
            return this.percentile75;
        }

        // get the standard deviation of the population
        public float GetStandardDeviation()
        {
            if (Single.IsNaN(this.standardDeviation) && (this.data.Count > 0))
            {
                // if effctive for efficiency, this can be changed to single pass standard deviation and included in SetData()
                float sum = 0.0F;
                foreach (float value in this.data)
                {
                    sum += (value - this.Mean) * (value - this.Mean);
                }
                this.standardDeviation = MathF.Sqrt(sum / (float)data.Count);
            }
            return this.standardDeviation;
        }

        [MemberNotNull(nameof(SummaryStatistics.data))]
        public void SetData(List<float> data)
        {
            this.data = data;
            // percentiles are calculated on demand
            this.percentile25 = Single.NaN;
            this.percentile75 = Single.NaN;
            this.median = Single.NaN;
            this.standardDeviation = Single.NaN;

            if (this.data.Count == 0)
            {
                this.Min = Single.NaN;
                this.Mean = Single.NaN;
                this.Max = Single.NaN;
                this.Sum = Single.NaN;
                return;
            }

            this.Min = Single.MaxValue;
            this.Max = Single.MinValue;

            this.Sum = 0.0F;
            foreach (float value in this.data)
            {
                this.Sum += value;
                this.Min = MathF.Min(value, this.Min);
                this.Max = MathF.Max(value, this.Max);
            }
            this.Mean = this.Sum / data.Count;
        }

        /** calculate Ranks.
          @param data values for N items,
          @param descending true: better ranks for lower values
          @return a vector that contains for the Nth data item the resulting rank.
          Example: in: {5, 2, 7, 5}
                   out: {2, 1, 4, 2}
          */
        //private static List<int> CalculateRanks(List<float> data, bool descending)
        //{
        //    // simple ranking algorithm.
        //    // we have "N" data-values.
        //    // rank := N - (N smaller or equal)
        //    List<int> ranks = new(data.Count);
        //    int n = data.Count;
        //    for (int i = 0; i < n; i++)
        //    {
        //        int smaller = 0;
        //        for (int j = 0; j < n; j++)
        //        {
        //            if (i == j)
        //            {
        //                continue;
        //            }
        //            if (data[j] <= data[i])
        //            {
        //                smaller++;
        //            }
        //        }
        //        if (descending) // better rank if lower value...
        //        {
        //            ranks.Add(smaller + 1);
        //        }
        //        else
        //        {
        //            ranks.Add(n - smaller);  // better rank if value is higher...
        //        }
        //    }
        //    return ranks;
        //}

        /** scale the data in such a way that the sum of all data items is "targetSum"
          */
        //private static void Normalize(List<float> data, float targetSum)
        //{
        //    float sum = 0.0;
        //    foreach (float value in data)
        //    {
        //        sum += value;
        //    }

        //    if (sum != 0)
        //    {
        //        float m = targetSum / sum;
        //        for (int index = 0; index < data.Count; ++index)
        //        {
        //            data[index] *= m;
        //        }
        //    }
        //}
    }
}
