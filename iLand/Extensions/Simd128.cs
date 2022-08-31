namespace iLand.Extensions
{
    public class Simd128
    {
        public const byte Blend32_0 = 0x1;
        public const byte Blend32_1 = 0x2;
        public const byte Blend32_2 = 0x4;
        public const byte Blend32_3 = 0x8;
        public const byte Blend32_123 = Blend32_1 | Blend32_2 | Blend32_3;
        public const byte Blend32_23 = Blend32_2 | Blend32_3;

        // public const byte MaskAllFalse = 0x0;
        public const byte MaskAllTrue = 0xf;

        public const byte Shuffle32_1to0 = 0x1 << 0 | 0x1 << 2 | 0x2 << 4 | 0x3 << 6; // copy element 1 to element 0, leaving elements 1, 2, and 3 unchanged
        public const byte Shuffle32_2to01 = 0x2 << 0 | 0x2 << 2 | 0x2 << 4 | 0x3 << 6; // copy element 2 to elements 0 and 1, leaving elements 2 and 3 unchanged
        public const byte Shuffle32_3to012 = 0x3 << 0 | 0x3 << 2 | 0x3 << 4 | 0x3 << 6; // broadcast element 3 to all elements of vector (copy element 3 to elements 0, 1 and 2, leaving elements 3 unchanged)

        public const int Width32 = 4;

        public static int RoundUpToWidth32(int minimumCapacity)
        {
            return Simd128.Width32 * (minimumCapacity / Simd128.Width32 + 1);
        }
    }
}
