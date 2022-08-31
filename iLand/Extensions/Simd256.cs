namespace iLand.Extensions
{
    internal static class Simd256
    {
        public const byte Blend32_0 = 0x01;
        public const byte Blend32_1 = 0x02;
        public const byte Blend32_2 = 0x04;
        public const byte Blend32_3 = 0x08;
        public const byte Blend32_4 = 0x10;
        public const byte Blend32_5 = 0x20;
        public const byte Blend32_6 = 0x40;
        public const byte Blend32_7 = 0x80;
        public const byte Blend32_12345 = Blend32_1 | Blend32_2 | Blend32_3 | Blend32_4 | Blend32_5;
        public const byte Blend32_23456 = Blend32_2 | Blend32_3 | Blend32_4 | Blend32_5 | Blend32_6;
        public const byte Blend32_34567 = Blend32_3 | Blend32_4 | Blend32_5 | Blend32_6 | Blend32_7;
        public const byte Blend32_567 = Blend32_5 | Blend32_6 | Blend32_7;
        public const byte Blend32_67 = Blend32_6 | Blend32_7;

        // public const byte MaskAllFalse = 0x00;
        public const byte MaskAllTrue = 0xff;

        // private const byte Permute128_1lower = 0x0;
        private const byte Permute128_1upper = 0x1;
        // private const byte Permute128_2lower = 0x2;
        private const byte Permute128_2upper = 0x3;
        public const byte Permute128_1upperTo1lower = Permute128_1upper << 0 | Permute128_2upper << 4; // move upper lane of 1 to lower lane, take upper lane of 2 as upper lane

        public const int Width32 = 8;
    }
}
