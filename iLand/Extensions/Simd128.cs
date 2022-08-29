namespace iLand.Extensions
{
    public class Simd128
    {
        public const int Width32 = 4;

        public static int RoundUpToWidth32(int minimumCapacity)
        {
            return Simd128.Width32 * (minimumCapacity / Simd128.Width32 + 1);
        }
    }
}
