using MersenneTwister;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;

namespace iLand.Tool
{
    public class RandomGenerator
    {
        // buffer undergoes thread safe refill when end is reached
        private readonly int[] buffer;
        private int bufferPosition = 0;
        private readonly Random pseudorandom;

        public RandomGenerator(bool mersenneTwister, int? seed)
        {
            this.buffer = new int[10 * 1024];
            this.bufferPosition = 0;

            if (seed.HasValue == false)
            {
                seed = RandomNumberGenerator.GetInt32(Int32.MaxValue);
            }
            if (mersenneTwister)
            {
                this.pseudorandom = MT64Random.Create(buffer[^1]);
            }
            else
            {
                this.pseudorandom = new Random(seed.Value);
            }
            this.RefillBuffer();
        }

        // returns a random number in [0, 1]
        public float GetRandomProbability() 
        {
            float value = ((float)this.GetRandomInteger() - Int32.MinValue) / (2.0F * Int32.MaxValue + 1.0F);
            Debug.Assert((value >= 0.0) && (value <= 1.0));
            return value;
        }

        // returns a random number in [0, toInclusive]
        public float GetRandomFloat(float toInclusive)
        { 
            return toInclusive * this.GetRandomProbability(); 
        }

        // get a random integer in [Int32.MinValue, Int32.MaxValue]
        public int GetRandomInteger()
        {
            int index = Interlocked.Increment(ref this.bufferPosition);
            while (index >= this.buffer.Length) // loop is unlikely to be entered and extremely unlikely to be reentered
            {
                lock (this.buffer)
                {
                    this.RefillBuffer();
                }
                index = Interlocked.Increment(ref this.bufferPosition);
            }
            return this.buffer[index];
        }

        public int GetRandomInteger(int maxValue) 
        { 
            return maxValue > 0 ? this.GetRandomInteger() % maxValue : 0; 
        }

        // GetRandomFloat() returns a random number from [p1, p2] -> p2 is a possible result!
        public float GetRandomFloat(float fromInclusive, float toInclusive)
        {
            return fromInclusive + this.GetRandomFloat(toInclusive - fromInclusive);
        }

        // return a random number from "from" to "to" (excluding 'to'), i.e. GetRandomInteger(3, 6) returns 3, 4 or 5.
        public int GetRandomInteger(int fromInclusive, int toExclusive)
        {
            return fromInclusive + this.GetRandomInteger(toExclusive - fromInclusive);
        }

        public double GetRandomNormal(double mean, double stddev)
        {
            // Return a real number from a normal (Gaussian) distribution with given
            // mean and standard deviation by polar form of Box-Muller transformation
            double x, y, r;
            do
            {
                x = 2.0 * this.GetRandomProbability() - 1.0;
                y = 2.0 * this.GetRandomProbability() - 1.0;
                r = x * x + y * y;
            }
            while (r >= 1.0 || r == 0.0);
            double s = Math.Sqrt(-2.0 * Math.Log(r) / r);
            return mean + x * s * stddev;
        }

        private void RefillBuffer()
        {
            if (this.bufferPosition < this.buffer.Length) // another thread might have already refilled....
            {
                return;
            }

            for (int index = 0; index < buffer.Length; ++index)
            {
                buffer[index] = this.pseudorandom.Next();
            }

            // reset read pointer after filling buffer to avoid other threads reusing parts of the buffer while it is being refilled
            this.bufferPosition = 0;
        }
    }
}
