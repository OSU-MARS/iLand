using System;

namespace iLand.Tools
{
    internal class RGenerators
    {
        /* initialize state to random bits  */
        private readonly uint[] state;
        /* init should also reset this to 0 */
        private int index;
        /* return 32 bit random number      */

        private int g_seed;
        private uint x, y, z;

        public RGenerators()
        {
            state = new uint[16];
        }

        private int Fastrand()
        {
            g_seed = (214013 * g_seed + 2531011); // BUGBUG: always same value
            return g_seed;
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
            a = state[index];
            c = state[(index + 13) & 15];
            b = a ^ c ^ (a << 16) ^ (c << 15);
            c = state[(index + 9) & 15];
            c ^= (c >> 11);
            a = state[index] = b ^ c;
            d = a ^ ((a << 5) & (uint)0xDA442D24UL);
            index = (index + 15) & 15;
            a = state[index];
            state[index] = a ^ b ^ d ^ (a << 2) ^ (b << 18) ^ (c << 28);
            return state[index];
        }

        // The Marsaglia's xorshf generator:
        // see: http://stackoverflow.com/questions/1640258/need-a-fast-random-generator-for-c and
        // http://www.cse.yorku.ca/~oz/marsaglia-rng.html
        //uint x=123456789, y=362436069, z=521288629;
        private uint Xorshf96()
        {
            //period 2^96-1
            uint t;
            x ^= x << 16;
            x ^= x >> 5;
            x ^= x << 1;

            t = x;
            x = y;
            y = z;
            z = t ^ x ^ y;

            return z;
        }

        public void Seed()
        {
            Random random = new Random();
            for (int i = 0; i < 16; i++)
            {
                state[i] = (uint)random.Next();
            }
            index = 0;
            // inits for the fast rand....
            g_seed = random.Next();
        }

        public void Seed(int oneSeed)
        {
            Random random = new Random(oneSeed);
            for (int i = 0; i < 16; i++)
            {
                state[i] = (uint)random.Next();
            }
            index = 0;
            g_seed = oneSeed;
        }
    }
}
