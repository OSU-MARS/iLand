using MersenneTwister;
using System;

namespace iLand.tools
{
    // a new set of numbers is generated for every 5*500000 = 2.500.000 numbers
    internal class RandomGenerator
    {
        private const int RANDOMGENERATORSIZE = 500000;
        private const int RANDOMGENERATORROTATIONS = 0;

        private static int[] mBuffer = new int[RANDOMGENERATORSIZE + 5];
        private static int mIndex = 0;
        private static int mRotationCount = RANDOMGENERATORROTATIONS + 1;
        private static int mRefillCounter = 0;
        private static ERandomGenerators mGeneratorType = ERandomGenerators.ergFastRandom;

        public enum ERandomGenerators
        {
            ergMersenneTwister,
            ergWellRNG512,
            ergXORShift96,
            ergFastRandom
        }

        public RandomGenerator()
        {
            seed(0);
            setGeneratorType(ERandomGenerators.ergMersenneTwister);
        }

        private static int next()
        {
            ++mIndex;
            if (mIndex > RANDOMGENERATORSIZE)
            {
                mRotationCount++;
                mIndex = 0;
                checkGenerator();
            }
            return mBuffer[mIndex];
        }

        /// set the type of the random generator that should be used.
        public static void setGeneratorType(ERandomGenerators gen)
        {
            mGeneratorType = gen;
            mRotationCount = RANDOMGENERATORROTATIONS + 1;
            mIndex = 0;
            mRefillCounter = 0;
        }

        public static void debugState(out int rIndex, out int rGeneration, out int rRefillCount)
        {
            rIndex = mIndex;
            rGeneration = mRotationCount;
            rRefillCount = mRefillCounter;
        }

        public static int debugNRandomNumbers()
        {
            return mIndex + RANDOMGENERATORSIZE * mRotationCount + (RANDOMGENERATORROTATIONS + 1) * RANDOMGENERATORSIZE * mRefillCounter;
        }

        /// call this function to check if we need to create new random numbers.
        /// this function is not reentrant! (e.g. call every year in the model)
        public static void checkGenerator()
        {
            if (mRotationCount > RANDOMGENERATORROTATIONS)
            {
                RandomGenerator.refill();
            }
        }

        public static void setup(ERandomGenerators gen, int oneSeed)
        {
            setGeneratorType(gen);
            seed(oneSeed);
            checkGenerator();
        }

        /// get a random value from [0., 1.]
        public static double rand() 
        { 
            return next() * (1.0 / 4294967295.0); 
        }

        public static double rand(double max_value)
        { 
            return max_value * rand(); 
        }

        /// get a random integer in [0,2^32-1]
        public static int randInt()
        { 
            return next(); 
        }

        public static int randInt(int max_value) 
        { 
            return max_value > 0 ? randInt() % max_value : 0; 
        }

        public static void refill()
        {
            // BUGBUG: check mRotationCount < RANDOMGENERATORROTATIONS 
            lock (mBuffer) // serialize access
            {
                if (mRotationCount < RANDOMGENERATORROTATIONS) // another thread might already succeeded in refilling....
                {
                    return;
                }

                RGenerators gen = new RGenerators();
                gen.seed(mBuffer[RANDOMGENERATORSIZE + 4]); // use the last value as seed for the next round....
                switch (mGeneratorType)
                {
                    case ERandomGenerators.ergMersenneTwister:
                        {
                            Random mersenne = MT64Random.Create(mBuffer[RANDOMGENERATORSIZE + 4]);
                            // qDebug() << "refill random numbers. seed" <<mBuffer[RANDOMGENERATORSIZE+4];
                            for (int i = 0; i < RANDOMGENERATORSIZE + 5; ++i)
                            {
                                mBuffer[i] = mersenne.Next();
                            }
                            break;
                        }
                    case ERandomGenerators.ergWellRNG512:
                        {

                            for (int i = 0; i < RANDOMGENERATORSIZE + 5; ++i)
                            {
                                mBuffer[i] = gen.random_function(0);
                            }
                            break;
                        }
                    case ERandomGenerators.ergXORShift96:
                        {
                            for (int i = 0; i < RANDOMGENERATORSIZE + 5; ++i)
                            {
                                mBuffer[i] = gen.random_function(1);
                            }
                            break;
                        }
                    case ERandomGenerators.ergFastRandom:
                        {
                            for (int i = 0; i < RANDOMGENERATORSIZE + 5; ++i)
                            {
                                mBuffer[i] = gen.random_function(2);
                            }
                            break;
                        }
                } // switch

                mIndex = 0; // reset the index
                mRotationCount = 0;
                mRefillCounter++;
            }
        }

        public static void seed(int oneSeed)
        {
            if (oneSeed == 0)
            {
                Random random = new Random();
                mBuffer[RANDOMGENERATORSIZE + 4] = random.Next();
            }
            else
            {
                mBuffer[RANDOMGENERATORSIZE + 4] = oneSeed; // set a specific seed as seed for the next round
            }
        }

        /// nrandom returns a random number from [p1, p2] -> p2 is a possible result!
        public static double nrandom(double p1, double p2)
        {
            return p1 + rand(p2 - p1);
            //return p1 + (p2-p1)*(rand()/double(RAND_MAX));
        }

        /// returns a random number in [0,1] (i.e.="1" is a possible result!)
        public static double drandom()
        {
            return rand();
            //return rand()/double(RAND_MAX);
        }

        /// return a random number from "from" to "to" (excluding 'to'.), i.e. irandom(3,6) results in 3, 4 or 5.
        public static int irandom(int from, int to)
        {
            return from + randInt(to - from);
            //return from +  rand()%(to-from);
        }

        public static double randNorm(double mean, double stddev)
        {
            // Return a real number from a normal (Gaussian) distribution with given
            // mean and standard deviation by polar form of Box-Muller transformation
            double x, y, r;
            do
            {
                x = 2.0 * rand() - 1.0;
                y = 2.0 * rand() - 1.0;
                r = x * x + y * y;
            }
            while (r >= 1.0 || r == 0.0);
            double s = Math.Sqrt(-2.0 * Math.Log(r) / r);
            return mean + x * s * stddev;
        }
    }
}
