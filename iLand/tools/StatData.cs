using iLand.Core;
using System;
using System.Collections.Generic;

namespace iLand.Tools
{
    /** StatData.
    * Helper class for statistics. This class calculates
    * from a double-vector relevant information used
    * for BoxPlots. */
    internal class StatData
    {
        private List<double> mData; // to allow late calculation of percentiles (e.g. a call to "median()".)
        private double mP25;
        private double mP75;
        private double mMedian;
        private double mSD; // standard deviation

        public double Sum { get; private set; } ///< sum of values
        public double Mean { get; private set; } ///< arithmetic mean
        public double Min { get; private set; } ///< minimum value
        public double Max { get; private set; } ///< maximum value

        public StatData() 
        {
            this.mData = new List<double>();
            this.mMedian = Double.NaN;
            this.mP25 = Double.NaN;
            this.mP75 = Double.NaN;
            this.mSD = Double.NaN;
            Calculate(); 
        }

        public StatData(List<double> data)
        {
            mData = data;
            Calculate();
        }

        public void SetData(List<double> data) 
        { 
            mData = data; 
            Calculate(); 
        }

        public double Median() { if (Double.IsNaN(mP25)) CalculatePercentiles(); return mMedian; } ///< 2nd quartil = median
        public double Percentile25() { if (Double.IsNaN(mP25)) CalculatePercentiles(); return mP25; } ///< 1st quartil
        public double Percentile75() { if (Double.IsNaN(mP75)) CalculatePercentiles(); return mP75; } ///< 3rd quartil
        public double StandardDev() { if (Double.IsNaN(mSD)) CalculateSD(); return mSD; } ///< get the standard deviation (of the population)

        private void CalculatePercentiles()
        {
            mP25 = Percentile(25);
            mP75 = Percentile(75);
            mMedian = Percentile(50);
        }

        public void Calculate()
        {
            if (mData.Count == 0)
            {
                Sum = mMedian = mP25 = mP75 = Mean = Min = Max = 0.0;
                return;
            }
            mP25 = Double.MaxValue;
            mP75 = Double.MaxValue;
            mMedian = Double.MaxValue;
            Min = Double.MaxValue;
            Max = -Double.MaxValue;
            mSD = Double.MaxValue;

            Sum = 0.0;
            foreach (double i in mData)
            {
                Sum += i;
                Min = Math.Min(i, Min);
                Max = Math.Max(i, Max);
            }
            Mean = Sum / (double)mData.Count;
            //qDebug() << QString("p25: {0} Median: {1} p75: {2} min: {3] max: {4}").arg(mP25).arg(mMedian).arg(mP75).arg(mMin).arg(mMax);
        }

        public double CalculateSD()
        {
            if (mData.Count == 0)
            {
                mSD = 0.0;
                return 0.0;
            }
            // calculate the standard deviation...
            double sum = 0.0;
            foreach (double i in mData)
            {
                sum += (i - Mean) * (i - Mean);
            }
            mSD = Math.Sqrt(sum / (double)mData.Count);
            return mSD;
        }

        public double Percentile(int percent)
        {
            // double *Values, int ValueCount,
            // code von: Fast median search: an ANSI C implementation, Nicolas Devillard, http://ndevilla.free.fr/median/median/index.html
            // algo. kommt von Wirth, hier nur an c++ angepasst.

            int perc = Global.Limit(percent, 1, 99);
            int ValueCount = mData.Count;
            int i, j, l, m, n, k;
            double x, temp;
            if (ValueCount == 0)
            {
                return 0;
            }

            n = ValueCount;
            // k ist der "Index" des gesuchten wertes
            if (perc != 50)
            {
                // irgendwelche perzentillen
                int d = 100 / ((perc > 50 ? (100 - perc) : perc));
                k = ValueCount / d;
                if (perc > 50)
                {
                    k = ValueCount - k - 1;
                }
            }
            else
            {
                // median
                if ((ValueCount & 1) == 1)  // gerade/ungerade?
                {
                    k = ValueCount / 2;  // mittlerer wert
                }
                else
                {
                    k = ValueCount / 2 - 1; // wert unter der mitte
                }
            }
            l = 0; m = n - 1;
            while (l < m)
            {
                x = mData[k];
                i = l;
                j = m;
                do
                {
                    while (mData[i] < x) i++;
                    while (x < mData[j]) j--;
                    if (i <= j)
                    {
                        //ELEM_SWAP(a[i],a[j]) ; swap elements:
                        temp = mData[i]; mData[i] = mData[j]; mData[j] = temp;
                        i++; j--;
                    }
                } while (i <= j);
                if (j < k) l = i;
                if (k < i) m = j;
            }
            return mData[k];
        }

        /** calculate Ranks.
          @param data values for N items,
          @param descending true: better ranks for lower values
          @return a vector that contains for the Nth data item the resulting rank.
          Example: in: {5, 2, 7, 5}
                   out: {2, 1, 4, 2}
          */
        public List<int> CalculateRanks(List<double> data, bool descending)
        {
            // simple ranking algorithm.
            // we have "N" data-values.
            // rank := N - (N smaller or equal)
            int i, j;
            int smaller;
            List<int> ranks = new List<int>(data.Count);
            int n = data.Count;
            for (i = 0; i < n; i++)
            {
                smaller = 0;
                for (j = 0; j < n; j++)
                {
                    if (i == j)
                    {
                        continue;
                    }
                    if (data[j] <= data[i])
                    {
                        smaller++;
                    }
                }
                if (descending) // better rank if lower value...
                {
                    ranks.Add(smaller + 1);
                }
                else
                {
                    ranks.Add(n - smaller);  // better rank if value is higher...
                }
            }
            return ranks;
        }

        /** scale the data in such a way that the sum of all data items is "targetSum"
          */
        public void Normalize(List<double> data, double targetSum)
        {
            double sum = 0.0;
            foreach (double i in data)
            {
                sum += i;
            }

            if (sum != 0)
            {
                double m = targetSum / sum;
                for (int index = 0; index < data.Count; ++index)
                {
                    data[index] *= m;
                }
            }
        }
    }
}
