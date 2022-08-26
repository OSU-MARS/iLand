using MersenneTwister;
using System;
using System.Diagnostics;

namespace iLand.Tool
{
    public class RandomGenerator
    {
        // buffer undergoes thread safe refill when end is reached
        private readonly int[] buffer;
        private int bufferPosition;
        private readonly Random pseudorandom;

        public RandomGenerator(bool mersenneTwister, int seed)
        {
            this.buffer = new int[10 * 1024]; // 10 kB
            this.bufferPosition = 0;
            this.pseudorandom = mersenneTwister ? MT64Random.Create(seed) : new Random(seed);

            this.RefillBuffer();
        }

        // returns a random number in [0, 1]
        public float GetRandomProbability() 
        {
            float value = ((float)this.GetRandomInteger() - Int32.MinValue) / (2.0F * Int32.MaxValue + 1.0F);
            Debug.Assert((value >= 0.0) && (value <= 1.0));
            return value;
        }

        // get a random integer in [Int32.MinValue, Int32.MaxValue]
        public int GetRandomInteger()
        {
            ++this.bufferPosition;
            if (this.bufferPosition >= this.buffer.Length) // loop is unlikely to be entered and extremely unlikely to be reentered
            {
                this.RefillBuffer();
            }
            return this.buffer[this.bufferPosition];
        }

        public int GetRandomInteger(int maxValue) 
        { 
            return maxValue > 0 ? this.GetRandomInteger() % maxValue : 0; 
        }

        // GetRandomFloat() returns a random number from [p1, p2] -> p2 is a possible result!
        public float GetRandomFloat(float fromInclusive, float toInclusive)
        {
            return fromInclusive + (toInclusive - fromInclusive) * this.GetRandomProbability();
        }

        // return a random number from "from" to "to" (excluding 'to'), i.e. GetRandomInteger(3, 6) returns 3, 4 or 5.
        public int GetRandomInteger(int fromInclusive, int toExclusive)
        {
            return fromInclusive + this.GetRandomInteger(toExclusive - fromInclusive);
        }

        public float GetRandomNormal(float mean, float stddev)
        {
            // Return a real number from a normal (Gaussian) distribution with given
            // mean and standard deviation by polar form of Box-Muller transformation
            float x, y, r;
            do
            {
                x = 2.0F * this.GetRandomProbability() - 1.0F;
                y = 2.0F * this.GetRandomProbability() - 1.0F;
                r = x * x + y * y;
            }
            while ((r >= 1.0F) || (r == 0.0F));
            float s = MathF.Sqrt(-2.0F * MathF.Log(r) / r);
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
