using System;

namespace iLand.Tools
{
    internal class RandomGenerators
    {
        /* initialize state to random bits  */
        private readonly uint[] wellrngState;
        /* init should also reset this to 0 */
        private int index;
        /* return 32 bit random number      */

        private int fastrandState;
        private uint x, y, z;

        public RandomGenerators()
        {
            wellrngState = new uint[16];
        }

        private int Fastrand()
        {
            fastrandState = 214013 * fastrandState + 2531011;
            return fastrandState;
        }

        public int RandomFunction(int type)
        {
            if (type == 0)
            {
                return unchecked((int)WellRng512());
            }
            if (type == 1)
            {
                return unchecked((int)Xorshf96());
            }
            if (type == 2)
            {
                return Fastrand();
            }
            return 0;
        }

        // see  http://www.lomont.org/Math/Papers/2008/Lomont_PRNG_2008.pdf
        // for details on the WellRNG512 algorithm
        public uint WellRng512()
        {
            uint a, b, c, d;
            a = wellrngState[index];
            c = wellrngState[(index + 13) & 15];
            b = a ^ c ^ (a << 16) ^ (c << 15);
            c = wellrngState[(index + 9) & 15];
            c ^= (c >> 11);
            a = wellrngState[index] = b ^ c;
            d = a ^ ((a << 5) & (uint)0xDA442D24UL);
            index = (index + 15) & 15;
            a = wellrngState[index];
            wellrngState[index] = a ^ b ^ d ^ (a << 2) ^ (b << 18) ^ (c << 28);
            return wellrngState[index];
        }

        // The Marsaglia's xorshf generator:
        // see: http://stackoverflow.com/questions/1640258/need-a-fast-random-generator-for-c and
        // http://www.cse.yorku.ca/~oz/marsaglia-rng.html
        //uint x=123456789, y=362436069, z=521288629;
        private uint Xorshf96()
        {
            //period 2^96-1
            x ^= x << 16;
            x ^= x >> 5;
            x ^= x << 1;

            uint t = x;
            x = y;
            y = z;
            z = t ^ x ^ y;

            return z;
        }

        public void SetSeed()
        {
            Random random = new();
            for (int index = 0; index < this.wellrngState.Length; ++index)
            {
                this.wellrngState[index] = (uint)random.Next();
            }
            this.index = 0;
            // inits for the fast rand....
            this.fastrandState = random.Next();
        }

        public void SetSeed(int seed)
        {
            Random random = new(seed);
            for (int index = 0; index < this.wellrngState.Length; ++index)
            {
                this.wellrngState[index] = (uint)random.Next();
            }
            this.index = 0;
            this.fastrandState = seed;
        }
    }
}
