using MersenneTwister;
using System;
using System.Diagnostics;

namespace iLand.Tools
{
    // a new set of numbers is generated for every 5*500000 = 2.500.000 numbers
    internal class RandomGenerator
    {
        private const int RandomGeneratorSize = 500000;

        private static readonly int[] mBuffer = new int[RandomGeneratorSize + 5];
        private static int mIndex = 0;
        private static int mRotationCount = 1;
        private static int mRefillCounter = 0;
        private static RandomGenerators mGeneratorType = RandomGenerators.Fast;

        public enum RandomGenerators
        {
            MersenneTwister,
            WellRng512,
            XorShift96,
            Fast
        }

        public RandomGenerator()
        {
            Seed(0);
            SetGeneratorType(RandomGenerators.MersenneTwister);
        }

        /// set the type of the random generator that should be used.
        public static void SetGeneratorType(RandomGenerators gen)
        {
            mGeneratorType = gen;
            mRotationCount = 1;
            mIndex = 0;
            mRefillCounter = 0;
        }

        public static void DebugState(out int rIndex, out int rGeneration, out int rRefillCount)
        {
            rIndex = mIndex;
            rGeneration = mRotationCount;
            rRefillCount = mRefillCounter;
        }

        public static int DebugNRandomNumbers()
        {
            return mIndex + RandomGeneratorSize * mRotationCount + RandomGeneratorSize * mRefillCounter;
        }

        /// call this function to check if we need to create new random numbers.
        /// this function is not reentrant! (e.g. call every year in the model)
        public static void CheckGenerator()
        {
            if (mRotationCount > 0)
            {
                RandomGenerator.Refill();
            }
        }

        public static void Setup(RandomGenerators gen, int oneSeed)
        {
            SetGeneratorType(gen);
            Seed(oneSeed);
            CheckGenerator();
        }

        /// returns a random number in [0,1] (i.e.="1" is a possible result!)
        public static double Random() 
        {
            double value = ((double)RandomInteger() - Int32.MinValue) / (2.0 * Int32.MaxValue + 1);
            Debug.Assert((value >= 0.0) && (value <= 1.0));
            return value;
        }

        public static double Random(double max_value)
        { 
            return max_value * Random(); 
        }

        /// get a random integer in [0,2^32-1]
        public static int RandomInteger()
        {
            ++mIndex;
            if (mIndex > RandomGeneratorSize)
            {
                mRotationCount++;
                mIndex = 0;
                CheckGenerator();
            }
            return mBuffer[mIndex];
        }

        public static int Random(int max_value) 
        { 
            return max_value > 0 ? RandomInteger() % max_value : 0; 
        }

        public static void Refill()
        {
            // BUGBUG: check mRotationCount < RANDOMGENERATORROTATIONS 
            lock (mBuffer) // serialize access
            {
                if (mRotationCount <= 0) // another thread might already succeeded in refilling....
                {
                    return;
                }

                RGenerators gen = new RGenerators();
                gen.Seed(mBuffer[RandomGeneratorSize + 4]); // use the last value as seed for the next round....
                switch (mGeneratorType)
                {
                    case RandomGenerators.MersenneTwister:
                        {
                            Random mersenne = MT64Random.Create(mBuffer[RandomGeneratorSize + 4]);
                            // qDebug() << "refill random numbers. seed" <<mBuffer[RANDOMGENERATORSIZE+4];
                            for (int i = 0; i < RandomGeneratorSize + 5; ++i)
                            {
                                mBuffer[i] = mersenne.Next();
                            }
                            break;
                        }
                    case RandomGenerators.WellRng512:
                        {

                            for (int i = 0; i < RandomGeneratorSize + 5; ++i)
                            {
                                mBuffer[i] = gen.RandomFunction(0);
                            }
                            break;
                        }
                    case RandomGenerators.XorShift96:
                        {
                            for (int i = 0; i < RandomGeneratorSize + 5; ++i)
                            {
                                mBuffer[i] = gen.RandomFunction(1);
                            }
                            break;
                        }
                    case RandomGenerators.Fast:
                        {
                            for (int i = 0; i < RandomGeneratorSize + 5; ++i)
                            {
                                mBuffer[i] = gen.RandomFunction(2);
                            }
                            break;
                        }
                } // switch

                mIndex = 0; // reset the index
                mRotationCount = 0;
                mRefillCounter++;
            }
        }

        public static void Seed(int oneSeed)
        {
            if (oneSeed == 0)
            {
                Random random = new Random();
                mBuffer[RandomGeneratorSize + 4] = random.Next();
            }
            else
            {
                mBuffer[RandomGeneratorSize + 4] = oneSeed; // set a specific seed as seed for the next round
            }
        }

        /// nrandom returns a random number from [p1, p2] -> p2 is a possible result!
        public static double Random(double p1, double p2)
        {
            return p1 + Random(p2 - p1);
            //return p1 + (p2-p1)*(rand()/double(RAND_MAX));
        }

        /// return a random number from "from" to "to" (excluding 'to'.), i.e. irandom(3,6) results in 3, 4 or 5.
        public static int Random(int from, int to)
        {
            return from + Random(to - from);
            //return from +  rand()%(to-from);
        }

        public static double RandNorm(double mean, double stddev)
        {
            // Return a real number from a normal (Gaussian) distribution with given
            // mean and standard deviation by polar form of Box-Muller transformation
            double x, y, r;
            do
            {
                x = 2.0 * Random() - 1.0;
                y = 2.0 * Random() - 1.0;
                r = x * x + y * y;
            }
            while (r >= 1.0 || r == 0.0);
            double s = Math.Sqrt(-2.0 * Math.Log(r) / r);
            return mean + x * s * stddev;
        }
    }
}
