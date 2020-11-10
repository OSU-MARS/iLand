using MersenneTwister;
using System;
using System.Diagnostics;

namespace iLand.Tools
{
    // a new set of numbers is generated for every 5*500000 = 2.500.000 numbers
    public class RandomGenerator
    {
        private const int RandomByteBufferSize = 500000;

        // thread safe; accessed under lock
        private readonly int[] mBuffer = new int[RandomByteBufferSize + 5];
        private int mIndex = 0;
        private int mRotationCount = 1;
        // private static int mRefillCounter = 0;
        private RandomGeneratorType mGeneratorType = RandomGeneratorType.MersenneTwister;

        public enum RandomGeneratorType
        {
            MersenneTwister,
            WellRng512,
            XorShift96,
            Fast
        }

        //public RandomGenerator()
        //{
        //    Seed(0);
        //    SetGeneratorType(RandomGenerators.MersenneTwister);
        //}

        //public void DebugState(out int rIndex, out int rGeneration, out int rRefillCount)
        //{
        //    rIndex = mIndex;
        //    rGeneration = mRotationCount;
        //    rRefillCount = mRefillCounter;
        //}

        //public int DebugNRandomNumbers()
        //{
        //    return mIndex + RandomGeneratorSize * mRotationCount + RandomGeneratorSize * mRefillCounter;
        //}

        /// call this function to check if we need to create new random numbers.
        /// this function is not reentrant! (e.g. call every year in the model)
        public void CheckGenerator()
        {
            if (mRotationCount > 0)
            {
                // BUGBUG: check mRotationCount < RANDOMGENERATORROTATIONS 
                lock (mBuffer) // serialize access
                {
                    if (mRotationCount <= 0) // another thread might have already refilled....
                    {
                        return;
                    }

                    RandomGenerators generator = new RandomGenerators();
                    generator.SetSeed(mBuffer[RandomByteBufferSize + 4]); // use the last value as seed for the next round....
                    switch (mGeneratorType)
                    {
                        case RandomGeneratorType.MersenneTwister:
                            {
                                Random mersenne = MT64Random.Create(mBuffer[RandomByteBufferSize + 4]);
                                // qDebug() << "refill random numbers. seed" <<mBuffer[RANDOMGENERATORSIZE+4];
                                for (int i = 0; i < RandomByteBufferSize + 5; ++i)
                                {
                                    mBuffer[i] = mersenne.Next();
                                }
                                break;
                            }
                        case RandomGeneratorType.WellRng512:
                            {
                                for (int i = 0; i < RandomByteBufferSize + 5; ++i)
                                {
                                    mBuffer[i] = generator.RandomFunction(0);
                                }
                                break;
                            }
                        case RandomGeneratorType.XorShift96:
                            {
                                for (int i = 0; i < RandomByteBufferSize + 5; ++i)
                                {
                                    mBuffer[i] = generator.RandomFunction(1);
                                }
                                break;
                            }
                        case RandomGeneratorType.Fast:
                            {
                                for (int i = 0; i < RandomByteBufferSize + 5; ++i)
                                {
                                    mBuffer[i] = generator.RandomFunction(2);
                                }
                                break;
                            }
                    } // switch

                    mIndex = 0; // reset the index
                    mRotationCount = 0;
                    //mRefillCounter++;
                }
            }
        }

        public void Setup(RandomGeneratorType gen, int? oneSeed)
        {
            this.mGeneratorType = gen;
            this.mRotationCount = 1;
            this.mIndex = 0;
            // mRefillCounter = 0;

            if (oneSeed.HasValue == false)
            {
                Random random = new Random();
                this.mBuffer[RandomGenerator.RandomByteBufferSize + 4] = random.Next();
            }
            else
            {
                this.mBuffer[RandomGenerator.RandomByteBufferSize + 4] = oneSeed.Value; // set a specific seed as seed for the next round
            }

            this.CheckGenerator();
        }

        /// returns a random number in [0,1] (i.e.="1" is a possible result!)
        public double GetRandomDouble() 
        {
            double value = ((double)GetRandomInteger() - Int32.MinValue) / (2.0 * Int32.MaxValue + 1);
            Debug.Assert((value >= 0.0) && (value <= 1.0));
            return value;
        }

        public double GetRandomDouble(double maxValue)
        { 
            return maxValue * GetRandomDouble(); 
        }

        /// get a random integer in [0,2^32-1]
        public int GetRandomInteger()
        {
            ++mIndex;
            if (mIndex > RandomByteBufferSize)
            {
                mRotationCount++;
                mIndex = 0;
                CheckGenerator();
            }
            return mBuffer[mIndex];
        }

        public int GetRandomInteger(int maxValue) 
        { 
            return maxValue > 0 ? GetRandomInteger() % maxValue : 0; 
        }

        /// nrandom returns a random number from [p1, p2] -> p2 is a possible result!
        public double GetRandomDouble(double p1, double p2)
        {
            return p1 + GetRandomDouble(p2 - p1);
            //return p1 + (p2-p1)*(rand()/double(RAND_MAX));
        }

        /// return a random number from "from" to "to" (excluding 'to'.), i.e. irandom(3,6) results in 3, 4 or 5.
        public int GetRandomInteger(int from, int to)
        {
            return from + GetRandomInteger(to - from);
            //return from +  rand()%(to-from);
        }

        public double GetRandomNormal(double mean, double stddev)
        {
            // Return a real number from a normal (Gaussian) distribution with given
            // mean and standard deviation by polar form of Box-Muller transformation
            double x, y, r;
            do
            {
                x = 2.0 * GetRandomDouble() - 1.0;
                y = 2.0 * GetRandomDouble() - 1.0;
                r = x * x + y * y;
            }
            while (r >= 1.0 || r == 0.0);
            double s = Math.Sqrt(-2.0 * Math.Log(r) / r);
            return mean + x * s * stddev;
        }
    }
}
